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

internal static class JoyConButtonNames
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
