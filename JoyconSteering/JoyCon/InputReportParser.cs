namespace JoyconSteering.JoyCon;

/// <summary>
/// Pure parser for Joy-Con standard input report 0x30 (64-byte buffer).
/// Splits HID byte interpretation from device I/O so unit tests can feed captured packets.
///
/// Layout reference: https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering
/// </summary>
internal static class InputReportParser
{
    // Accelerometer: int16; 1g ≈ 4096 LSB (factory FSR ±8g, 16-bit signed).
    public const double AccelToG = 1.0 / 4096.0;
    // Gyroscope: int16; ±2000 dps full-scale → 0.06103 dps/LSB.
    public const double GyroToDps = 0.06103;

    // Nominal stick center / range. Real Joy-Cons have factory calibration in SPI;
    // this is "good enough" until we read SPI cal.
    public const int StickCenterNom = 2048;
    public const int StickRangeNom = 1700;

    public const byte StandardReportId = 0x30;
    public const int ReportSize = 49;

    public static bool IsStandardReport(ReadOnlySpan<byte> buffer)
        => buffer.Length >= 1 && buffer[0] == StandardReportId;

    /// <summary>Battery level 0-8 from byte 2 upper nibble.</summary>
    public static int ParseBattery(ReadOnlySpan<byte> buffer) => (buffer[2] >> 4) & 0x0F;

    public static LeftJoyConButton ParseLeftButtons(ReadOnlySpan<byte> buffer)
    {
        byte shared = buffer[4];
        byte leftSide = buffer[5];
        LeftJoyConButton b = LeftJoyConButton.None;
        if ((leftSide & 0x01) != 0) b |= LeftJoyConButton.Down;
        if ((leftSide & 0x02) != 0) b |= LeftJoyConButton.Up;
        if ((leftSide & 0x04) != 0) b |= LeftJoyConButton.Right;
        if ((leftSide & 0x08) != 0) b |= LeftJoyConButton.Left;
        if ((leftSide & 0x10) != 0) b |= LeftJoyConButton.SrL;
        if ((leftSide & 0x20) != 0) b |= LeftJoyConButton.SlL;
        if ((leftSide & 0x40) != 0) b |= LeftJoyConButton.L;
        if ((leftSide & 0x80) != 0) b |= LeftJoyConButton.Zl;
        if ((shared & 0x01) != 0) b |= LeftJoyConButton.Minus;
        if ((shared & 0x08) != 0) b |= LeftJoyConButton.Stick;
        if ((shared & 0x20) != 0) b |= LeftJoyConButton.Capture;
        return b;
    }

    /// <param name="isLeft">Pick stick offset: left stick starts at byte 6, right at byte 9.</param>
    public static (double X, double Y) ParseStick(ReadOnlySpan<byte> buffer, bool isLeft)
    {
        int off = isLeft ? 6 : 9;
        int xRaw = buffer[off] | ((buffer[off + 1] & 0x0F) << 8);
        int yRaw = (buffer[off + 1] >> 4) | (buffer[off + 2] << 4);
        double x = Math.Clamp((xRaw - StickCenterNom) / (double)StickRangeNom, -1.0, 1.0);
        double y = Math.Clamp((yRaw - StickCenterNom) / (double)StickRangeNom, -1.0, 1.0);
        return (x, y);
    }

    public static ImuSample ParseImu(ReadOnlySpan<byte> buffer, int sampleIndex)
    {
        int off = 13 + sampleIndex * 12;
        short ax = (short)(buffer[off]      | (buffer[off + 1] << 8));
        short ay = (short)(buffer[off + 2]  | (buffer[off + 3] << 8));
        short az = (short)(buffer[off + 4]  | (buffer[off + 5] << 8));
        short gx = (short)(buffer[off + 6]  | (buffer[off + 7] << 8));
        short gy = (short)(buffer[off + 8]  | (buffer[off + 9] << 8));
        short gz = (short)(buffer[off + 10] | (buffer[off + 11] << 8));
        return new ImuSample(
            ax * AccelToG, ay * AccelToG, az * AccelToG,
            gx * GyroToDps, gy * GyroToDps, gz * GyroToDps);
    }

    public static JoyConState ParseStandard(ReadOnlySpan<byte> buffer, bool isLeft)
    {
        if (buffer.Length < ReportSize)
            throw new ArgumentException($"Buffer too short: {buffer.Length} < {ReportSize}", nameof(buffer));
        if (buffer[0] != StandardReportId)
            throw new ArgumentException($"Not a 0x30 report (got 0x{buffer[0]:X2})", nameof(buffer));

        var (sx, sy) = ParseStick(buffer, isLeft);
        return new JoyConState(
            ParseLeftButtons(buffer),
            sx, sy,
            ParseBattery(buffer),
            ParseImu(buffer, 0),
            ParseImu(buffer, 1),
            ParseImu(buffer, 2));
    }
}
