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
    private static JoyConState State(LeftJoyConButton buttons = LeftJoyConButton.None, double stickX = 0, double stickY = 0)
        => new(buttons, stickX, stickY, Battery: 8, default, default, default);

    private static AppConfig CfgWithButtons(ThrottleBrakeMode tb = ThrottleBrakeMode.Stick, double stickDead = 0.15)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["up"] = 1, ["down"] = 2, ["left"] = 3, ["right"] = 4,
            ["l"] = 5, ["zl"] = 6, ["minus"] = 7, ["stick"] = 8,
            ["sl"] = 9, ["sr"] = 10, ["capture"] = 11,
        };
        return new AppConfig
        {
            ButtonMap = map,
            ThrottleBrake = tb,
            StickDeadzone = stickDead,
        };
    }

    [Fact]
    public void Apply_WritesSteeringAndFlushes()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons()).Apply(0.42, State(), fake);
        Assert.Equal(0.42, fake.Steering, 3);
        Assert.Equal(1, fake.FlushCount);
    }

    [Fact]
    public void Apply_ClampsSteeringToUnitRange()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons()).Apply(2.5, State(), fake);
        Assert.Equal(1.0, fake.Steering, 3);
        new WheelOutputMapper(CfgWithButtons()).Apply(-2.5, State(), fake);
        Assert.Equal(-1.0, fake.Steering, 3);
    }

    [Fact]
    public void StickMode_PositiveY_DrivesThrottle()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, State(stickY: 0.575), fake);
        // (0.575 - 0.15) / (1 - 0.15) = 0.5
        Assert.Equal(0.5, fake.Throttle, 3);
        Assert.Equal(0.0, fake.Brake, 3);
    }

    [Fact]
    public void StickMode_NegativeY_DrivesBrake()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, State(stickY: -0.575), fake);
        Assert.Equal(0.5, fake.Brake, 3);
        Assert.Equal(0.0, fake.Throttle, 3);
    }

    [Fact]
    public void StickMode_WithinDeadzone_BothZero()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(stickDead: 0.15)).Apply(0, State(stickY: 0.1), fake);
        Assert.Equal(0, fake.Throttle);
        Assert.Equal(0, fake.Brake);
    }

    [Fact]
    public void ButtonsMode_LEqualsThrottle_ZlEqualsBrake()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.Buttons))
            .Apply(0, State(buttons: LeftJoyConButton.L), fake);
        Assert.Equal(1.0, fake.Throttle);
        Assert.Equal(0.0, fake.Brake);

        fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.Buttons))
            .Apply(0, State(buttons: LeftJoyConButton.Zl), fake);
        Assert.Equal(0.0, fake.Throttle);
        Assert.Equal(1.0, fake.Brake);
    }

    [Fact]
    public void NoneMode_BothZero_RegardlessOfInput()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons(ThrottleBrakeMode.None))
            .Apply(0, State(stickY: 0.9, buttons: LeftJoyConButton.L | LeftJoyConButton.Zl), fake);
        Assert.Equal(0, fake.Throttle);
        Assert.Equal(0, fake.Brake);
    }

    [Fact]
    public void Buttons_MapJoyConToVJoy_ByConfig()
    {
        var fake = new FakeWheelOutput();
        new WheelOutputMapper(CfgWithButtons())
            .Apply(0, State(buttons: LeftJoyConButton.Up | LeftJoyConButton.Minus), fake);
        Assert.True(fake.Buttons[1]);   // up
        Assert.True(fake.Buttons[7]);   // minus
        Assert.False(fake.Buttons[2]);  // down
        Assert.False(fake.Buttons[8]);  // stick
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
        new WheelOutputMapper(cfg).Apply(0, State(buttons: LeftJoyConButton.Up), fake);
        Assert.Empty(fake.Buttons);
    }
}
