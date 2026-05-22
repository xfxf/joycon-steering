using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace JoyconSteering.Output;

/// <summary>
/// Locates vJoyInterface.dll at runtime. vJoy installs to C:\Program Files\vJoy\x64\
/// but does not reliably add itself to PATH, so a plain [DllImport] often fails with
/// DllNotFoundException. This resolver tries the app folder, the known install paths,
/// and the registry InstallLocation key before giving up.
/// </summary>
internal static class VJoyLibraryResolver
{
    private const string LibraryName = "vJoyInterface.dll";

    /// <summary>Call once at startup, before any P/Invoke to vJoyInterface.</summary>
    public static void Register()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(VJoyOutput).Assembly,
            (name, _, _) => name.Equals(LibraryName, StringComparison.OrdinalIgnoreCase)
                ? TryLoadFromCandidates()
                : IntPtr.Zero);
    }

    /// <summary>Public for tests: which paths we probe, in order.</summary>
    public static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, LibraryName);

        // Standard install locations.
        yield return @"C:\Program Files\vJoy\x64\" + LibraryName;
        yield return @"C:\Program Files\vJoy\" + LibraryName;
        yield return @"C:\Program Files (x86)\vJoy\x64\" + LibraryName;
        yield return @"C:\Program Files (x86)\vJoy\" + LibraryName;

        // Registry-reported install location (covers non-default installs).
        foreach (var dir in TryReadRegistryInstallPaths())
        {
            yield return Path.Combine(dir, "x64", LibraryName);
            yield return Path.Combine(dir, LibraryName);
        }
    }

    private static IntPtr TryLoadFromCandidates()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path)) continue;
            if (NativeLibrary.TryLoad(path, out var handle)) return handle;
        }
        return IntPtr.Zero;
    }

    private static IEnumerable<string> TryReadRegistryInstallPaths()
    {
        // Try a few keys the vJoy installer has used over the years.
        var candidates = new (RegistryHive Hive, string Path, string ValueName)[]
        {
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\vJoy", "InstallLocation"),
            (RegistryHive.LocalMachine, @"Software\Headsoft\vJoy",                                    "InstallLocation"),
            (RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\vJoy", "InstallLocation"),
        };
        foreach (var (hive, path, valueName) in candidates)
        {
            string? value = null;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = root.OpenSubKey(path);
                value = key?.GetValue(valueName) as string;
            }
            catch { /* swallow; registry access can throw */ }
            if (!string.IsNullOrWhiteSpace(value)) yield return value!;
        }
    }
}
