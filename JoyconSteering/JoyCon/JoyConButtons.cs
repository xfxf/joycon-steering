namespace JoyconSteering.JoyCon;

[Flags]
public enum LeftJoyConButton : uint
{
    None    = 0,
    Down    = 1 << 0,
    Up      = 1 << 1,
    Right   = 1 << 2,
    Left    = 1 << 3,
    SrL     = 1 << 4,
    SlL     = 1 << 5,
    L       = 1 << 6,
    Zl      = 1 << 7,
    Minus   = 1 << 8,
    Stick   = 1 << 9,
    Capture = 1 << 10,
}

public static class JoyConButtonNames
{
    public static LeftJoyConButton FromName(string name) => name.ToLowerInvariant() switch
    {
        "up" => LeftJoyConButton.Up,
        "down" => LeftJoyConButton.Down,
        "left" => LeftJoyConButton.Left,
        "right" => LeftJoyConButton.Right,
        "l" => LeftJoyConButton.L,
        "zl" => LeftJoyConButton.Zl,
        "minus" => LeftJoyConButton.Minus,
        "stick" => LeftJoyConButton.Stick,
        "sl" => LeftJoyConButton.SlL,
        "sr" => LeftJoyConButton.SrL,
        "capture" => LeftJoyConButton.Capture,
        _ => LeftJoyConButton.None,
    };
}

/// <summary>
/// Side-aware button helpers — pick the appropriate enum and bit by joy-con side,
/// so config strings like "stick" or "l" resolve to the right physical button no
/// matter which side the user has set as steering or pedals.
/// </summary>
public static class JoyConSideButtons
{
    /// <summary>Resolve a config-style button name to its bitmask on the given side.
    /// Returns 0 if the name doesn't exist on that side.</summary>
    public static uint NameToBit(Config.JoyConSide side, string name) => side switch
    {
        Config.JoyConSide.Left => (uint)JoyConButtonNames.FromName(name),
        Config.JoyConSide.Right => (uint)RightJoyConButtonNames.FromName(name),
        _ => 0u,
    };

    /// <summary>True if the named button is set in the raw button word for that side.</summary>
    public static bool IsPressed(Config.JoyConSide side, uint rawButtons, string name)
    {
        uint bit = NameToBit(side, name);
        return bit != 0 && (rawButtons & bit) != 0;
    }

    /// <summary>The "throttle" shoulder button for the given side (L on left, R on right).</summary>
    public static uint ThrottleShoulderBit(Config.JoyConSide side) => side == Config.JoyConSide.Left
        ? (uint)LeftJoyConButton.L
        : (uint)RightJoyConButton.R;

    /// <summary>The "brake" shoulder button for the given side (ZL on left, ZR on right).</summary>
    public static uint BrakeShoulderBit(Config.JoyConSide side) => side == Config.JoyConSide.Left
        ? (uint)LeftJoyConButton.Zl
        : (uint)RightJoyConButton.Zr;
}
