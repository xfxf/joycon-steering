namespace JoyconSteering.Config;

public enum SteeringAxis { Auto, Roll, Pitch, Yaw, Wheel, Tilt }
public enum ThrottleBrakeMode { Stick, Buttons, None }
public enum JoyConSide { Left, Right }

public sealed record AppConfig
{
    public JoyConSide Side { get; init; }
    public uint VJoyDeviceId { get; init; }

    public SteeringAxis Axis { get; init; }
    public double RangeDegrees { get; init; }
    public double DeadzoneDegrees { get; init; }
    public double SmoothingMs { get; init; }
    public bool Invert { get; init; }

    public ThrottleBrakeMode ThrottleBrake { get; init; }
    public double StickDeadzone { get; init; }

    public Dictionary<string, int> ButtonMap { get; init; } = new();
    public string RecenterButton { get; init; } = "stick";
    public double AutoRecenterIdleSeconds { get; init; }

    public double MadgwickBeta { get; init; }

    public static AppConfig Load(string path)
    {
        var ini = IniReader.Load(path);

        var side = ini.GetString("device", "joycon_side", "left").ToLowerInvariant() == "right"
            ? JoyConSide.Right : JoyConSide.Left;

        var axisStr = ini.GetString("steering", "axis", "auto").ToLowerInvariant();
        var axis = axisStr switch
        {
            "roll" => SteeringAxis.Roll,
            "pitch" => SteeringAxis.Pitch,
            "yaw" => SteeringAxis.Yaw,
            "wheel" => SteeringAxis.Wheel,
            "tilt" => SteeringAxis.Tilt,
            _ => SteeringAxis.Auto,
        };

        var tbStr = ini.GetString("throttle_brake", "mode", "stick").ToLowerInvariant();
        var tb = tbStr switch
        {
            "buttons" => ThrottleBrakeMode.Buttons,
            "none" => ThrottleBrakeMode.None,
            _ => ThrottleBrakeMode.Stick,
        };

        var buttonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "up", "down", "left", "right", "l", "zl", "minus", "stick", "sl", "sr", "capture" })
            buttonMap[name] = ini.GetInt("buttons", name, 0);

        return new AppConfig
        {
            Side = side,
            VJoyDeviceId = (uint)ini.GetInt("device", "vjoy_device_id", 1),
            Axis = axis,
            RangeDegrees = ini.GetDouble("steering", "range_degrees", 350),
            DeadzoneDegrees = ini.GetDouble("steering", "deadzone_degrees", 1.5),
            SmoothingMs = ini.GetDouble("steering", "smoothing_ms", 10),
            Invert = ini.GetBool("steering", "invert", true),
            ThrottleBrake = tb,
            StickDeadzone = ini.GetDouble("throttle_brake", "stick_deadzone", 0.15),
            ButtonMap = buttonMap,
            RecenterButton = ini.GetString("recenter", "button", "stick").ToLowerInvariant(),
            AutoRecenterIdleSeconds = ini.GetDouble("recenter", "auto_recenter_idle_seconds", 0),
            MadgwickBeta = ini.GetDouble("fusion", "madgwick_beta", 0.05),
        };
    }
}
