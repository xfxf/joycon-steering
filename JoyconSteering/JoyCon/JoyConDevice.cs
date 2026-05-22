using HidSharp;

namespace JoyconSteering.JoyCon;

internal sealed class JoyConDevice : IDisposable
{
    private const int NintendoVid = 0x057E;
    private const int LeftJoyConPid = 0x2006;
    private const int RightJoyConPid = 0x2007;

    private readonly HidStream _stream;
    private readonly bool _isLeft;
    private byte _packetCounter;
    private readonly byte[] _outBuf;
    private readonly byte[] _readBuf;

    public bool IsLeft => _isLeft;

    public static JoyConDevice OpenLeft() => Open(LeftJoyConPid, isLeft: true);
    public static JoyConDevice OpenRight() => Open(RightJoyConPid, isLeft: false);

    private static JoyConDevice Open(int pid, bool isLeft)
    {
        var list = DeviceList.Local;
        var dev = list.GetHidDevices(NintendoVid, pid).FirstOrDefault()
                  ?? throw new InvalidOperationException(
                      $"No Joy-Con found (VID 0x057E PID 0x{pid:X4}). Pair it via Windows Bluetooth first.");

        // Joy-Con BT HID declares specific report sizes; using larger buffers makes Windows
        // reject the write with "The parameter is incorrect." Honour the device's declared sizes.
        int outSize = dev.GetMaxOutputReportLength();
        int inSize = dev.GetMaxInputReportLength();
        if (outSize <= 0) outSize = 49;
        if (inSize <= 0) inSize = 49;

        var stream = dev.Open();
        stream.ReadTimeout = 200;
        stream.WriteTimeout = 200;
        return new JoyConDevice(stream, isLeft, outSize, inSize);
    }

    private JoyConDevice(HidStream stream, bool isLeft, int outSize, int inSize)
    {
        _stream = stream;
        _isLeft = isLeft;
        _outBuf = new byte[outSize];
        _readBuf = new byte[inSize];
    }

    public void Initialize()
    {
        // Subcommand 0x40: enable IMU.
        SendSubcommand(0x40, new byte[] { 0x01 });
        // Subcommand 0x03: set input report mode to 0x30 (standard full IMU).
        SendSubcommand(0x03, new byte[] { 0x30 });
        // Subcommand 0x30: set LEDs (light the leftmost player LED so the user knows it's live).
        SendSubcommand(0x30, new byte[] { 0x01 });
    }

    private void SendSubcommand(byte subcmd, byte[] args)
    {
        Array.Clear(_outBuf);
        _outBuf[0] = 0x01;                       // Report ID: subcommand
        _outBuf[1] = unchecked((byte)(_packetCounter++ & 0x0F));
        // Rumble neutral (8 bytes)
        _outBuf[2] = 0x00; _outBuf[3] = 0x01; _outBuf[4] = 0x40; _outBuf[5] = 0x40;
        _outBuf[6] = 0x00; _outBuf[7] = 0x01; _outBuf[8] = 0x40; _outBuf[9] = 0x40;
        _outBuf[10] = subcmd;
        Buffer.BlockCopy(args, 0, _outBuf, 11, args.Length);
        _stream.Write(_outBuf, 0, _outBuf.Length);
    }

    /// <summary>Block for one input report, parse it, return state. Throws on timeout.</summary>
    public JoyConState Read()
    {
        int n = _stream.Read(_readBuf, 0, _readBuf.Length);
        if (n < InputReportParser.ReportSize) throw new IOException($"Short HID read: {n} bytes");
        if (!InputReportParser.IsStandardReport(_readBuf))
            return Read(); // skip subcommand replies (0x21) and re-read
        return InputReportParser.ParseStandard(_readBuf, _isLeft);
    }

    public void Dispose() => _stream.Dispose();
}
