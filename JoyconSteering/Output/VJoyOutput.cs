using System.Runtime.InteropServices;

namespace JoyconSteering.Output;

/// <summary>
/// Real vJoy output. P/Invokes vJoyInterface.dll directly so we don't ship the SDK wrappers.
/// The vJoy driver must be installed (https://sourceforge.net/projects/vjoystick/) and the
/// target device configured with axes X, Y, Rz and at least 16 buttons.
///
/// Axes used:
///   X  → steering (-1..+1 mapped to 0..32767 with center 16383/16384)
///   Y  → throttle (0..1 → 0..32767)
///   Rz → brake    (0..1 → 0..32767)
/// </summary>
public sealed class VJoyOutput : IWheelOutput, IDisposable
{
    private const uint HID_USAGE_X = 0x30;
    private const uint HID_USAGE_Y = 0x31;
    private const uint HID_USAGE_RZ = 0x35;
    private const int AxisMin = 0;
    private const int AxisMax = 32767;
    private const int AxisCenter = AxisMax / 2;

    private const int VJD_STAT_OWN = 0;
    private const int VJD_STAT_FREE = 1;

    private readonly uint _deviceId;
    private bool _acquired;

    public VJoyOutput(uint deviceId) => _deviceId = deviceId;

    public void Open()
    {
        bool enabled;
        try { enabled = vJoyEnabled(); }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException(
                "Could not locate vJoyInterface.dll. Install vJoy from " +
                "https://sourceforge.net/projects/vjoystick/ — the default install " +
                @"path is C:\Program Files\vJoy\.");
        }
        if (!enabled)
            throw new InvalidOperationException("vJoy driver is not enabled. Install vJoy and create device 1 with X, Y, Rz axes + ≥16 buttons.");

        int status = GetVJDStatus(_deviceId);
        if (status != VJD_STAT_FREE && status != VJD_STAT_OWN)
            throw new InvalidOperationException($"vJoy device {_deviceId} is not available (status {status}). Open vJoyConf and make sure it's configured.");

        if (!AcquireVJD(_deviceId))
            throw new InvalidOperationException($"Failed to acquire vJoy device {_deviceId}.");

        _acquired = true;
        ResetVJD(_deviceId);
    }

    public void SetSteering(double value)
    {
        int v = AxisCenter + (int)Math.Round(Math.Clamp(value, -1.0, 1.0) * AxisCenter);
        SetAxis(v, _deviceId, HID_USAGE_X);
    }

    public void SetThrottle(double value)
        => SetAxis((int)Math.Round(Math.Clamp(value, 0.0, 1.0) * AxisMax), _deviceId, HID_USAGE_Y);

    public void SetBrake(double value)
        => SetAxis((int)Math.Round(Math.Clamp(value, 0.0, 1.0) * AxisMax), _deviceId, HID_USAGE_RZ);

    public void SetButton(int buttonNumber, bool pressed)
        => SetBtn(pressed, _deviceId, (byte)buttonNumber);

    public void Flush() { /* vJoy commits per-call; no batching needed */ }

    public void Dispose()
    {
        if (_acquired)
        {
            try { ResetVJD(_deviceId); } catch { /* swallow */ }
            try { RelinquishVJD(_deviceId); } catch { /* swallow */ }
            _acquired = false;
        }
    }

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool vJoyEnabled();

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetVJDStatus(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool AcquireVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RelinquishVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ResetVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SetAxis(int Value, uint rID, uint Axis);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SetBtn(bool Value, uint rID, byte nBtn);
}
