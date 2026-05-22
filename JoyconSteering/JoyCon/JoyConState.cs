namespace JoyconSteering.JoyCon;

public readonly record struct JoyConState(
    LeftJoyConButton Buttons,
    double StickX,        // -1.0 .. +1.0 (centered)
    double StickY,        // -1.0 .. +1.0 (up positive)
    int Battery,          // 0..8 raw value (>>4 of byte 2)
    byte Timer,           // packet counter, increments by 3 per report (5ms/tick)
    ImuSample Sample0,    // oldest of the 3 samples in this report (5ms ago)
    ImuSample Sample1,
    ImuSample Sample2);   // newest
