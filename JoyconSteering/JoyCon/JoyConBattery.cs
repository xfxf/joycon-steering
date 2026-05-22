namespace JoyconSteering.JoyCon;

/// <summary>
/// Joy-Con battery decoding.
///
/// Byte 2's upper nibble of report 0x30 encodes battery as:
///   bit 0 (low): 1 if charging
///   bits 1-3:    level index 0..4
///
/// Discrete levels in dekuNukem's reverse-engineering notes:
///   8 = full      (level 4 → 100%)
///   6 = medium    (level 3 →  75%)
///   4 = low       (level 2 →  50%)
///   2 = critical  (level 1 →  25%)
///   0 = empty     (level 0 →   0%)
/// Adding 1 to any of those means "+ charging" (e.g. 9 = full + charging).
/// </summary>
public static class JoyConBattery
{
    public static int Percent(int rawNibble)
    {
        int level = (rawNibble >> 1) & 0x07;
        if (level > 4) level = 4;
        return level * 25;
    }

    public static bool IsCharging(int rawNibble) => (rawNibble & 1) == 1;
}
