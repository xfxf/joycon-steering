using JoyconSteering.Config;
using Xunit;

namespace JoyconSteering.Tests;

public class PedalConfigTests
{
    private static string WriteTempIni(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"joycon-pedal-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData("stick",         ThrottleBrakeMode.Stick)]
    [InlineData("buttons",       ThrottleBrakeMode.Buttons)]
    [InlineData("pedal_stick",   ThrottleBrakeMode.PedalStick)]
    [InlineData("pedal_buttons", ThrottleBrakeMode.PedalButtons)]
    [InlineData("pedal_tilt",    ThrottleBrakeMode.PedalTilt)]
    [InlineData("none",          ThrottleBrakeMode.None)]
    [InlineData("garbage",       ThrottleBrakeMode.PedalStick)]  // fall back to new default
    public void Mode_ParsesAllValues(string ini, ThrottleBrakeMode expected)
    {
        var path = WriteTempIni($"[throttle_brake]\nmode = {ini}\n");
        var cfg = AppConfig.Load(path);
        Assert.Equal(expected, cfg.ThrottleBrake);
    }

    [Fact]
    public void PedalButtons_DefaultsAreReasonable()
    {
        var path = WriteTempIni("");
        var cfg = AppConfig.Load(path);
        Assert.Equal("zr", cfg.PedalThrottleButton);
        Assert.Equal("r",  cfg.PedalBrakeButton);
    }

    [Fact]
    public void PedalButtons_ParsesOverrides()
    {
        var path = WriteTempIni("""
            [pedal_buttons]
            throttle = a
            brake = b
            """);
        var cfg = AppConfig.Load(path);
        Assert.Equal("a", cfg.PedalThrottleButton);
        Assert.Equal("b", cfg.PedalBrakeButton);
    }

    [Fact]
    public void PedalTilt_DefaultsAreReasonable()
    {
        var path = WriteTempIni("");
        var cfg = AppConfig.Load(path);
        Assert.Equal(SteeringAxis.Auto, cfg.PedalTiltAxis);
        Assert.Equal(45.0, cfg.PedalTiltRangeDegrees);
        Assert.Equal(8.0,  cfg.PedalTiltDeadzoneDegrees);
        Assert.False(cfg.PedalTiltInvert);
        Assert.Equal("stick", cfg.PedalRecenterButton);
    }

    [Fact]
    public void PedalTilt_ParsesOverrides()
    {
        var path = WriteTempIni("""
            [pedal_tilt]
            axis = roll
            range_degrees = 60
            deadzone_degrees = 5
            invert = true
            recenter_button = plus
            """);
        var cfg = AppConfig.Load(path);
        Assert.Equal(SteeringAxis.Roll, cfg.PedalTiltAxis);
        Assert.Equal(60.0, cfg.PedalTiltRangeDegrees);
        Assert.Equal(5.0,  cfg.PedalTiltDeadzoneDegrees);
        Assert.True(cfg.PedalTiltInvert);
        Assert.Equal("plus", cfg.PedalRecenterButton);
    }

    [Fact]
    public void PedalsEnabled_ReturnsTrue_ForAllPedalModes()
    {
        Assert.True(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.PedalStick));
        Assert.True(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.PedalButtons));
        Assert.True(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.PedalTilt));
        Assert.False(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.Stick));
        Assert.False(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.Buttons));
        Assert.False(PedalsConfigHelper.RequiresPedalJoyCon(ThrottleBrakeMode.None));
    }

    [Theory]
    [InlineData(JoyConSide.Left,  JoyConSide.Right)]
    [InlineData(JoyConSide.Right, JoyConSide.Left)]
    public void PedalSide_IsOppositeOfSteeringSide(JoyConSide steering, JoyConSide expected)
    {
        Assert.Equal(expected, PedalsConfigHelper.PedalSideFor(steering));
    }
}
