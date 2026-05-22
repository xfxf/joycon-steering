using System.Globalization;

namespace JoyconSteering.Config;

internal sealed class IniReader
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public static IniReader Load(string path)
    {
        var ini = new IniReader();
        string currentSection = "";
        ini._sections[currentSection] = new(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!ini._sections.ContainsKey(currentSection))
                    ini._sections[currentSection] = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            var commentStart = value.IndexOf(';');
            if (commentStart >= 0) value = value[..commentStart].TrimEnd();
            ini._sections[currentSection][key] = value;
        }
        return ini;
    }

    public string GetString(string section, string key, string fallback)
        => _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : fallback;

    public int GetInt(string section, string key, int fallback)
        => int.TryParse(GetString(section, key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public double GetDouble(string section, string key, double fallback)
        => double.TryParse(GetString(section, key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public bool GetBool(string section, string key, bool fallback)
    {
        var s = GetString(section, key, "").ToLowerInvariant();
        return s switch
        {
            "true" or "yes" or "1" or "on" => true,
            "false" or "no" or "0" or "off" => false,
            _ => fallback
        };
    }
}
