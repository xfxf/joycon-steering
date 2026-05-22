namespace JoyconSteering.Config;

public enum SteeringAxis { Auto, Roll, Pitch, Yaw, Wheel, Tilt }
public enum ThrottleBrakeMode { Stick, Buttons, PedalButtons, PedalTilt, None }
public enum JoyConSide { Left, Right }

public static class PedalsConfigHelper
{
    /// <summary>True when the configured mode reads from the OTHER Joy-Con (not the steering one).</summary>
    public static bool RequiresPedalJoyCon(ThrottleBrakeMode mode)
        => mode is ThrottleBrakeMode.PedalButtons or ThrottleBrakeMode.PedalTilt;

    /// <summary>The pedal Joy-Con is always the one opposite to the steering Joy-Con.</summary>
    public static JoyConSide PedalSideFor(JoyConSide steeringSide)
        => steeringSide == JoyConSide.Left ? JoyConSide.Right : JoyConSide.Left;
}

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

    // ── Pedal Joy-Con: button mode ──────────────────────────────────────────
    public string PedalThrottleButton { get; init; } = "zr";
    public string PedalBrakeButton    { get; init; } = "r";

    // ── Pedal Joy-Con: tilt mode ────────────────────────────────────────────
    public SteeringAxis PedalTiltAxis { get; init; } = SteeringAxis.Auto;
    public double PedalTiltRangeDegrees    { get; init; } = 45;
    public double PedalTiltDeadzoneDegrees { get; init; } = 8;
    public bool   PedalTiltInvert          { get; init; }
    public string PedalRecenterButton      { get; init; } = "home";

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
            "pedal_buttons" => ThrottleBrakeMode.PedalButtons,
            "pedal_tilt" => ThrottleBrakeMode.PedalTilt,
            "none" => ThrottleBrakeMode.None,
            _ => ThrottleBrakeMode.Stick,
        };

        var pedalAxisStr = ini.GetString("pedal_tilt", "axis", "auto").ToLowerInvariant();
        var pedalAxis = pedalAxisStr switch
        {
            "roll" => SteeringAxis.Roll,
            "pitch" => SteeringAxis.Pitch,
            "yaw" => SteeringAxis.Yaw,
            "wheel" => SteeringAxis.Wheel,
            "tilt" => SteeringAxis.Tilt,
            _ => SteeringAxis.Auto,
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
            PedalThrottleButton = ini.GetString("pedal_buttons", "throttle", "zr").ToLowerInvariant(),
            PedalBrakeButton    = ini.GetString("pedal_buttons", "brake",    "r").ToLowerInvariant(),
            PedalTiltAxis = pedalAxis,
            PedalTiltRangeDegrees    = ini.GetDouble("pedal_tilt", "range_degrees", 45),
            PedalTiltDeadzoneDegrees = ini.GetDouble("pedal_tilt", "deadzone_degrees", 8),
            PedalTiltInvert          = ini.GetBool("pedal_tilt", "invert", false),
            PedalRecenterButton      = ini.GetString("pedal_tilt", "recenter_button", "home").ToLowerInvariant(),
            ButtonMap = buttonMap,
            RecenterButton = ini.GetString("recenter", "button", "stick").ToLowerInvariant(),
            AutoRecenterIdleSeconds = ini.GetDouble("recenter", "auto_recenter_idle_seconds", 0),
            MadgwickBeta = ini.GetDouble("fusion", "madgwick_beta", 0.05),
        };
    }
}
