namespace JoyconSteering.JoyCon;

/// <summary>Parsed right Joy-Con report 0x30 — same shape as <see cref="JoyConState"/>
/// but with the right side's button decoding and the right stick.</summary>
public readonly record struct RightJoyConState(
    RightJoyConButton Buttons,
    double StickX,
    double StickY,
    int Battery,
    byte Timer,
    ImuSample Sample0,
    ImuSample Sample1,
    ImuSample Sample2);
