using System.Runtime.InteropServices;
using System.Windows.Forms;
using JoyconSteering.Config;
using JoyconSteering.Output;
using JoyconSteering.Ui;

namespace JoyconSteering;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    public static int Main(string[] args)
    {
        VJoyLibraryResolver.Register();
        Logger.Banner($"Startup pid={Environment.ProcessId} args=[{string.Join(' ', args)}]");

        string iniPath = ResolveIniPath(args);

        // --diagnose: headless self-check. Print to parent console + log file. Exit.
        if (args.Any(a => a.Equals("--diagnose", StringComparison.OrdinalIgnoreCase)))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            // Force a newline so output isn't glued to the shell prompt.
            Console.Out.WriteLine();
            int code = Diagnostics.Run(File.Exists(iniPath) ? iniPath : null, Console.Out);
            Logger.Info($"diagnose exit={code}");
            return code;
        }

        ApplicationConfiguration.Initialize();

        if (!File.Exists(iniPath)) TryWriteDefaultIni(iniPath);

        AppConfig config;
        try { config = AppConfig.Load(iniPath); }
        catch (Exception ex)
        {
            Logger.Error("Failed to load config", ex);
            MessageBox.Show($"Failed to load config:\n{iniPath}\n\n{ex.Message}",
                "JoyconSteering", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        Logger.Info($"Loaded config: side={config.Side} vjoy={config.VJoyDeviceId} axis={config.Axis} range={config.RangeDegrees}");
        var ctx = new TrayAppContext(iniPath, config);
        Application.Run(ctx);
        Logger.Info("Clean exit");
        return 0;
    }

    private static string ResolveIniPath(string[] args)
    {
        var positional = args.FirstOrDefault(a => !a.StartsWith('-'));
        return positional ?? Path.Combine(AppContext.BaseDirectory, "App.ini");
    }

    private static void TryWriteDefaultIni(string path)
    {
        try
        {
            var src = Path.Combine(AppContext.BaseDirectory, "App.ini");
            if (src != path && File.Exists(src)) File.Copy(src, path);
        }
        catch { /* swallow */ }
    }
}
