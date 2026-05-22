using System.Runtime.InteropServices;
using HidSharp;
using JoyconSteering.Config;
using JoyconSteering.Output;

namespace JoyconSteering;

/// <summary>
/// Self-check that exercises every external dependency the app needs at startup,
/// without requiring the user to be holding the controller. Designed to be run
/// from the command line via --diagnose for headless validation.
/// </summary>
internal static class Diagnostics
{
    private const int NintendoVid = 0x057E;
    private const int LeftPid = 0x2006;
    private const int RightPid = 0x2007;

    public enum Outcome { Ok, Warn, Fail }

    public sealed record Check(string Name, Outcome Outcome, string Detail, string? Hint = null);

    /// <summary>Returns exit code: 0 = all OK, 1 = any failure.</summary>
    public static int Run(string? iniPath, TextWriter writer)
    {
        Logger.Banner("Diagnose run");

        var checks = new List<Check>
        {
            CheckVJoyDll(),
            CheckVJoyEnabled(),
            CheckVJoyDevice(iniPath),
            CheckBluetoothHidDevices(),
            CheckLeftJoyConPresent(),
            CheckIniFile(iniPath),
        };

        writer.WriteLine();
        writer.WriteLine("JoyconSteering self-check");
        writer.WriteLine("Log: " + Logger.LogFilePath);
        writer.WriteLine(new string('-', 60));
        foreach (var c in checks)
        {
            string tag = c.Outcome switch { Outcome.Ok => "[OK]   ", Outcome.Warn => "[WARN] ", _ => "[FAIL] " };
            writer.WriteLine($"{tag} {c.Name}");
            writer.WriteLine($"        {c.Detail}");
            if (c.Hint is not null) writer.WriteLine($"        → {c.Hint}");
            Logger.Info($"check {c.Outcome} {c.Name}: {c.Detail}");
        }
        writer.WriteLine(new string('-', 60));
        int fails = checks.Count(c => c.Outcome == Outcome.Fail);
        int warns = checks.Count(c => c.Outcome == Outcome.Warn);
        writer.WriteLine($"Result: {fails} failure(s), {warns} warning(s)");
        return fails == 0 ? 0 : 1;
    }

    private static Check CheckVJoyDll()
    {
        var found = VJoyLibraryResolver.CandidatePaths().FirstOrDefault(File.Exists);
        if (found is not null)
            return new Check("vJoyInterface.dll located", Outcome.Ok, found);
        return new Check("vJoyInterface.dll located", Outcome.Fail,
            "DLL not found in any standard location.",
            "Install vJoy from https://sourceforge.net/projects/vjoystick/ (default install path C:\\Program Files\\vJoy\\).");
    }

    private static Check CheckVJoyEnabled()
    {
        try
        {
            if (VJoyInterop.vJoyEnabled())
                return new Check("vJoy driver enabled", Outcome.Ok, "Driver is loaded and active.");
            return new Check("vJoy driver enabled", Outcome.Fail,
                "vJoy DLL loaded but the driver is disabled.",
                "Open vJoyConf and ensure the vJoy service is running.");
        }
        catch (Exception ex)
        {
            return new Check("vJoy driver enabled", Outcome.Fail,
                $"Could not call vJoyEnabled(): {ex.Message}",
                "Confirm vJoy was installed successfully; try reinstalling.");
        }
    }

    private static Check CheckVJoyDevice(string? iniPath)
    {
        uint deviceId = 1;
        try
        {
            if (iniPath is not null && File.Exists(iniPath))
                deviceId = AppConfig.Load(iniPath).VJoyDeviceId;
        }
        catch { /* fall back to default 1 */ }

        try
        {
            int status = VJoyInterop.GetVJDStatus(deviceId);
            string statusName = status switch
            {
                0 => "OWN (already owned by another process)",
                1 => "FREE (ready to acquire)",
                2 => "BUSY",
                3 => "MISS (device not configured in vJoyConf)",
                4 => "UNKN",
                _ => $"raw={status}",
            };
            if (status == 1 || status == 0)
                return new Check($"vJoy device {deviceId} configured", Outcome.Ok, $"Status: {statusName}.");
            return new Check($"vJoy device {deviceId} configured", Outcome.Fail,
                $"Status: {statusName}.",
                $"Open vJoyConf, enable Device {deviceId}, add X/Y/Rz axes and ≥16 buttons, click Apply.");
        }
        catch (Exception ex)
        {
            return new Check($"vJoy device {deviceId} configured", Outcome.Fail,
                $"Could not query device status: {ex.Message}");
        }
    }

    private static Check CheckBluetoothHidDevices()
    {
        try
        {
            int total = DeviceList.Local.GetHidDevices().Count();
            return new Check("HID device enumeration", Outcome.Ok,
                $"Windows reports {total} HID device(s) total.");
        }
        catch (Exception ex)
        {
            return new Check("HID device enumeration", Outcome.Fail,
                $"Failed to enumerate HID devices: {ex.Message}");
        }
    }

    private static Check CheckLeftJoyConPresent()
    {
        try
        {
            var left = DeviceList.Local.GetHidDevices(NintendoVid, LeftPid).ToList();
            var right = DeviceList.Local.GetHidDevices(NintendoVid, RightPid).ToList();
            if (left.Count > 0)
                return new Check("Left Joy-Con paired and visible", Outcome.Ok,
                    $"Found {left.Count} left Joy-Con device(s).");
            if (right.Count > 0)
                return new Check("Left Joy-Con paired and visible", Outcome.Warn,
                    $"Found a RIGHT Joy-Con ({right.Count}) but no left.",
                    "Switch [device] joycon_side = right in Settings, or pair the left Joy-Con.");
            return new Check("Left Joy-Con paired and visible", Outcome.Fail,
                "No Joy-Con found via Windows Bluetooth.",
                "Open Settings → Bluetooth & devices → Add device. On the Joy-Con, hold the small sync button between SR and SL until the four lights race horizontally. Wait for 'Joy-Con (L)' to pair.");
        }
        catch (Exception ex)
        {
            return new Check("Left Joy-Con paired and visible", Outcome.Fail,
                $"HID query failed: {ex.Message}");
        }
    }

    private static Check CheckIniFile(string? iniPath)
    {
        if (iniPath is null || !File.Exists(iniPath))
            return new Check("App.ini present", Outcome.Warn,
                "Config file missing; defaults will be used.",
                "Run the app once with no flags to create App.ini in the exe directory.");
        try
        {
            _ = AppConfig.Load(iniPath);
            return new Check("App.ini present and parseable", Outcome.Ok, iniPath);
        }
        catch (Exception ex)
        {
            return new Check("App.ini present and parseable", Outcome.Fail,
                $"Parse error: {ex.Message}",
                $"Edit {iniPath} to fix the syntax, or delete it to reset to defaults.");
        }
    }
}

/// <summary>P/Invoke shim used by Diagnostics (without acquiring a vJoy device).</summary>
internal static class VJoyInterop
{
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool vJoyEnabled();

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetVJDStatus(uint rID);
}
