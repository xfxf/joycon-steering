using JoyconSteering.Config;
using Xunit;

namespace JoyconSteering.Tests;

public class AppConfigTests
{
    private static string WriteTempIni(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"joycon-cfg-{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Defaults_AppliedForMissingSections()
    {
        var path = WriteTempIni("");
        var cfg = AppConfig.Load(path);

        Assert.Equal(JoyConSide.Left, cfg.Side);
        Assert.Equal(1u, cfg.VJoyDeviceId);
        Assert.Equal(SteeringAxis.Auto, cfg.Axis);
        Assert.Equal(350.0, cfg.RangeDegrees);
        Assert.Equal(1.5, cfg.DeadzoneDegrees);
        Assert.Equal(10.0, cfg.SmoothingMs);
        Assert.True(cfg.Invert);
        Assert.Equal(ThrottleBrakeMode.PedalTilt, cfg.ThrottleBrake);
        Assert.Equal(0.15, cfg.StickDeadzone);
        Assert.Equal("stick", cfg.RecenterButton);
        Assert.Equal(0.0, cfg.AutoRecenterIdleSeconds);
        Assert.Equal(0.05, cfg.MadgwickBeta);
    }

    [Fact]
    public void ParsesFullConfig()
    {
        var path = WriteTempIni("""
            [device]
            joycon_side = right
            vjoy_device_id = 3
            [steering]
            axis = roll
            range_degrees = 90
            deadzone_degrees = 2.0
            smoothing_ms = 20
            invert = true
            [throttle_brake]
            mode = buttons
            stick_deadzone = 0.25
            [buttons]
            up = 11
            zl = 12
            [recenter]
            button = minus
            auto_recenter_idle_seconds = 5.0
            [fusion]
            madgwick_beta = 0.08
            """);

        var cfg = AppConfig.Load(path);
        Assert.Equal(JoyConSide.Right, cfg.Side);
        Assert.Equal(3u, cfg.VJoyDeviceId);
        Assert.Equal(SteeringAxis.Roll, cfg.Axis);
        Assert.Equal(90.0, cfg.RangeDegrees);
        Assert.Equal(2.0, cfg.DeadzoneDegrees);
        Assert.Equal(20.0, cfg.SmoothingMs);
        Assert.True(cfg.Invert);
        Assert.Equal(ThrottleBrakeMode.Buttons, cfg.ThrottleBrake);
        Assert.Equal(0.25, cfg.StickDeadzone);
        Assert.Equal(11, cfg.ButtonMap["up"]);
        Assert.Equal(12, cfg.ButtonMap["zl"]);
        Assert.Equal("minus", cfg.RecenterButton);
        Assert.Equal(5.0, cfg.AutoRecenterIdleSeconds);
        Assert.Equal(0.08, cfg.MadgwickBeta);
    }

    [Theory]
    [InlineData("none", ThrottleBrakeMode.None)]
    [InlineData("stick", ThrottleBrakeMode.Stick)]
    [InlineData("buttons", ThrottleBrakeMode.Buttons)]
    [InlineData("garbage", ThrottleBrakeMode.PedalTilt)] // unknown → new default
    public void ThrottleBrakeMode_ParsesAndDefaults(string mode, ThrottleBrakeMode expected)
    {
        var path = WriteTempIni($"[throttle_brake]\nmode = {mode}\n");
        var cfg = AppConfig.Load(path);
        Assert.Equal(expected, cfg.ThrottleBrake);
    }

    [Theory]
    [InlineData("auto", SteeringAxis.Auto)]
    [InlineData("roll", SteeringAxis.Roll)]
    [InlineData("pitch", SteeringAxis.Pitch)]
    [InlineData("yaw", SteeringAxis.Yaw)]
    [InlineData("garbage", SteeringAxis.Auto)]
    public void SteeringAxis_ParsesAndDefaults(string axis, SteeringAxis expected)
    {
        var path = WriteTempIni($"[steering]\naxis = {axis}\n");
        var cfg = AppConfig.Load(path);
        Assert.Equal(expected, cfg.Axis);
    }
}
