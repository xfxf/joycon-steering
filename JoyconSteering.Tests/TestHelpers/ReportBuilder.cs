namespace JoyconSteering.Tests.TestHelpers;

/// <summary>
/// Builds synthetic Joy-Con 0x30 input reports for parser tests.
/// 49-byte payload starting with 0x30 report ID.
/// </summary>
internal static class ReportBuilder
{
    public static byte[] EmptyStandard()
    {
        var buf = new byte[64];
        buf[0] = 0x30;
        return buf;
    }

    public static byte[] WithBattery(byte[] buf, int level0to8)
    {
        // byte 2 upper nibble = battery
        buf[2] = (byte)((buf[2] & 0x0F) | ((level0to8 & 0x0F) << 4));
        return buf;
    }

    public static byte[] WithLeftButton(byte[] buf, byte sideMask, byte sharedMask)
    {
        buf[4] |= sharedMask;
        buf[5] |= sideMask;
        return buf;
    }

    public static byte[] WithLeftStick(byte[] buf, int rawX, int rawY)
    {
        // Encoding inverse of parser: byte[6]=rawX low8, byte[7]=(rawX>>8 nibble) | (rawY low nibble<<4), byte[8]=rawY>>4
        buf[6] = (byte)(rawX & 0xFF);
        buf[7] = (byte)(((rawX >> 8) & 0x0F) | ((rawY & 0x0F) << 4));
        buf[8] = (byte)((rawY >> 4) & 0xFF);
        return buf;
    }

    public static byte[] WithImuSample(byte[] buf, int sampleIndex, short ax, short ay, short az, short gx, short gy, short gz)
    {
        int off = 13 + sampleIndex * 12;
        WriteI16(buf, off + 0,  ax);
        WriteI16(buf, off + 2,  ay);
        WriteI16(buf, off + 4,  az);
        WriteI16(buf, off + 6,  gx);
        WriteI16(buf, off + 8,  gy);
        WriteI16(buf, off + 10, gz);
        return buf;
    }

    private static void WriteI16(byte[] buf, int off, short v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }
}
