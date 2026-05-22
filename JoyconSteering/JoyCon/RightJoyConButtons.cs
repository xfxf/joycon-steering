namespace JoyconSteering.JoyCon;

[Flags]
public enum RightJoyConButton : uint
{
    None  = 0,
    Y     = 1 << 0,
    X     = 1 << 1,
    B     = 1 << 2,
    A     = 1 << 3,
    SrR   = 1 << 4,
    SlR   = 1 << 5,
    R     = 1 << 6,
    Zr    = 1 << 7,
    Plus  = 1 << 8,
    Stick = 1 << 9,  // R-Stick click
    Home  = 1 << 10,
}

public static class RightJoyConButtonNames
{
    public static RightJoyConButton FromName(string name) => name.ToLowerInvariant() switch
    {
        "y" => RightJoyConButton.Y,
        "x" => RightJoyConButton.X,
        "b" => RightJoyConButton.B,
        "a" => RightJoyConButton.A,
        "r" => RightJoyConButton.R,
        "zr" => RightJoyConButton.Zr,
        "sl" => RightJoyConButton.SlR,
        "sr" => RightJoyConButton.SrR,
        "plus" => RightJoyConButton.Plus,
        "stick" => RightJoyConButton.Stick,
        "home" => RightJoyConButton.Home,
        _ => RightJoyConButton.None,
    };
}
