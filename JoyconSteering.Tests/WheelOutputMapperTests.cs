using JoyconSteering.Config;
using JoyconSteering.JoyCon;
using JoyconSteering.Output;
using Xunit;

namespace JoyconSteering.Tests;

internal sealed class FakeWheelOutput : IWheelOutput
{
    public double Steering, Throttle, Brake;
    public Dictionary<int, bool> Buttons = new();
    public int FlushCount;

    public void SetSteering(double value) => Steering = value;
    public void SetThrottle(double value) => Throttle = value;
    public void SetBrake(double value) => Brake = value;
    public void SetButton(int n, bool pressed) => Buttons[n] = pressed;
    public void Flush() => FlushCount++;
}

public class WheelOutputMapperTests
{
    private static AppConfig CfgWithButtons(
        ThrottleBrakeMode tb = ThrottleBrakeMode.Stick,
        double stickDead = 0.15,
        JoyConSide side = JoyConSide.Left)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = 1, ["down"] = 2, ["left"] = 3, ["right"] = 4,
            ["l"] = 5, ["zl"] = 6, ["minus"] = 7, ["stick"] = 8,
            ["sl"] = 9, ["sr"] = 10, ["capture"] = 11,
        };
        return new AppConfig
        {
            Side = side,
            ButtonMap = map,
            ThrottleBrake = tb,
            StickDeadzone = stickDead,
        };
    }

    [Fact]
    public void Apply_WritesSteeringAndFlushes()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons()).Apply(0.42, 0, 0, 0, fake);
        Assert.Equal(0.42, fake.Steering, 3);
        Assert.Equal(1, fake.FlushCount);
    }

    [Fact]
    public void Apply_ClampsSteeringToUnitRange()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons()).Apply(2.5, 0, 0, 0, fake);
        Assert.Equal(1.0, fake.Steering, 3);
        new WheelOutputMapper(CfgWithButtons()).Apply(-2.5, 0, 0, 0, fake);
        Assert.Equal(-1.0, fake.Steering, 3);
    }

    [Fact]
    public void StickMode_PositiveY_DrivesThrottle()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, 0, 0.575, 0, fake);
        // (0.575 - 0.15) / (1 - 0.15) = 0.5
        Assert.Equal(0.5, fake.Throttle, 3);
        Assert.Equal(0.0, fake.Brake, 3);
    }

    [Fact]
    public void StickMode_NegativeY_DrivesBrake()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, 0, -0.575, 0, fake);
        Assert.Equal(0.5, fake.Brake, 3);
        Assert.Equal(0.0, fake.Throttle, 3);
    }

    [Fact]
    public void StickMode_WithinDeadzone_BothZero()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, 0, 0.1, 0, fake);
        Assert.Equal(0, fake.Throttle);
        Assert.Equal(0, fake.Brake);
    }

    [Fact]
    public void ButtonsMode_Left_LEqualsThrottle_ZlEqualsBrake()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.Buttons))
            .Apply(0, 0, 0, (uint)LeftJoyConButton.L, fake);
        Assert.Equal(1.0, fake.Throttle);
        Assert.Equal(0.0, fake.Brake);

        fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.Buttons))
            .Apply(0, 0, 0, (uint)LeftJoyConButton.Zl, fake);
        Assert.Equal(0.0, fake.Throttle);
        Assert.Equal(1.0, fake.Brake);
    }

    [Fact]
    public void ButtonsMode_Right_REqualsThrottle_ZrEqualsBrake()
    {
        // With Side=Right, the shoulder mapping shifts to R/ZR so the user gets
        // the same conceptual layout regardless of which joycon is steering.
        var cfg = CfgWithButtons(ThrottleBrakeMode.Buttons, side: JoyConSide.Right);
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0, 0, 0, (uint)RightJoyConButton.R, fake);
        Assert.Equal(1.0, fake.Throttle);
        Assert.Equal(0.0, fake.Brake);

        fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0, 0, 0, (uint)RightJoyConButton.Zr, fake);
        Assert.Equal(0.0, fake.Throttle);
        Assert.Equal(1.0, fake.Brake);
    }

    [Fact]
    public void NoneMode_BothZero_RegardlessOfInput()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.None))
            .Apply(0, 0, 0.9, (uint)LeftJoyConButton.L | (uint)LeftJoyConButton.Zl, fake);
        Assert.Equal(0, fake.Throttle);
        Assert.Equal(0, fake.Brake);
    }

    [Fact]
    public void Buttons_MapJoyConToVJoy_ByConfig_Left()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons())
            .Apply(0, 0, 0, (uint)LeftJoyConButton.Up | (uint)LeftJoyConButton.Minus, fake);
        Assert.True(fake.Buttons[1]);   // up
        Assert.True(fake.Buttons[7]);   // minus
        Assert.False(fake.Buttons[2]);  // down
        Assert.False(fake.Buttons[8]);  // stick
    }

    [Fact]
    public void Buttons_MapJoyConToVJoy_ByConfig_Right()
    {
        // Right-side steering with a button map that uses right-side names.
        var cfg = CfgWithButtons(side: JoyConSide.Right) with
        {
            ButtonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = 1, ["b"] = 2, ["zr"] = 3, ["home"] = 4,
            },
        };
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg)
            .Apply(0, 0, 0, (uint)RightJoyConButton.A | (uint)RightJoyConButton.Zr, fake);
        Assert.True(fake.Buttons[1]);   // a
        Assert.True(fake.Buttons[3]);   // zr
        Assert.False(fake.Buttons[2]);  // b
        Assert.False(fake.Buttons[4]);  // home
    }

    [Fact]
    public void StickAxisX_DrivesThrottleBrake_WhenConfigured()
    {
        var cfg = CfgWithButtons() with { StickAxis = StickAxis.X };
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0, 0.575, 0, 0, fake);
        // X positive → throttle (matches "right = throttle" label)
        Assert.Equal(0.5, fake.Throttle, 3);
        Assert.Equal(0.0, fake.Brake);
    }

    [Fact]
    public void ExternalPedals_OverrideStickComputation_InPedalMode()
    {
        var cfg = CfgWithButtons(ThrottleBrakeMode.PedalButtons);
        var fake = new FakeWheelOutput();
        // Stick is pushed, but mode is pedal_buttons → external pedals must win.
        new WheelOutputMapper(cfg).Apply(0, 0, 0.9, 0, fake, externalPedals: (0.42, 0.0));
        Assert.Equal(0.42, fake.Throttle, 3);
        Assert.Equal(0.0,  fake.Brake);
    }

    [Fact]
    public void ExternalPedals_Ignored_InStickMode()
    {
        var cfg = CfgWithButtons(ThrottleBrakeMode.Stick);
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0, 0, 0.575, 0, fake, externalPedals: (0.9, 0.1));
        // Mode is stick, external is ignored — pedals computed from stick Y.
        Assert.Equal(0.5, fake.Throttle, 3);
        Assert.Equal(0.0, fake.Brake);
    }

    [Fact]
    public void ExternalPedals_NullInPedalMode_Defaults_To_Zero()
    {
        var cfg = CfgWithButtons(ThrottleBrakeMode.PedalTilt);
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(cfg).Apply(0, 0, 0.9, 0, fake, externalPedals: null);
        // Pedal joycon not connected yet → no override → safe zero.
        Assert.Equal(0.0, fake.Throttle);
        Assert.Equal(0.0, fake.Brake);
    }

    [Fact]
    public void ZeroButtonMapping_IsIgnored()
    {
        var fake = new FakeWheelOutput();
        var cfg = new AppConfig
        {
            ButtonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["up"] = 0 },
            ThrottleBrake = ThrottleBrakeMode.None,
        };
        new WheelOutputMapper(cfg).Apply(0, 0, 0, (uint)LeftJoyConButton.Up, fake);
        Assert.Empty(fake.Buttons);
    }
}
