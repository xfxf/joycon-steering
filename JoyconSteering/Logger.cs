using System.Globalization;
using System.Text;

namespace JoyconSteering;

/// <summary>
/// Append-only file logger. Writes to %LOCALAPPDATA%\JoyconSteering\joyconsteering.log.
/// Thread-safe. Designed so the user (or a diagnose run) can tail/read the file at any time.
/// </summary>
public static class Logger
{
    private static readonly object _gate = new();
    private static string? _path;

    public static string LogFilePath
    {
        get
        {
            if (_path is not null) return _path;
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JoyconSteering");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "joyconsteering.log");
            return _path;
        }
    }

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex)
        => Write("ERROR", message + "\n    " + ex.GetType().Name + ": " + ex.Message);

    public static void Banner(string title)
    {
        var line = "===== " + title + " =====";
        Write("INFO ", line);
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(' ').Append(level).Append(' ').Append(message)
                .Append(Environment.NewLine)
                .ToString();
            lock (_gate)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch { /* logging must never throw */ }
    }
}
