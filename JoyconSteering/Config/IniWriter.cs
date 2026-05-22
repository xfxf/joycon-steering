namespace JoyconSteering.Config;

/// <summary>
/// Comment-preserving INI updater. Reads an existing INI file line-by-line and
/// rewrites only the values of known (section, key) pairs. Unknown lines —
/// comments, blank lines, keys we don't update — are passed through unchanged.
///
/// If a known key doesn't exist in the file, it is appended to its section.
/// If a known section doesn't exist, the section header + keys are appended.
/// </summary>
public static class IniWriter
{
    /// <summary>
    /// Apply <paramref name="updates"/> to the INI at <paramref name="path"/>.
    /// Keys are matched case-insensitively. Values are written verbatim.
    /// </summary>
    public static void Update(string path, IReadOnlyDictionary<(string Section, string Key), string> updates)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var remaining = new Dictionary<(string, string), string>(updates, KeyComparer.Instance);

        string currentSection = "";
        // Track the last line index belonging to each section so we can append missing keys there.
        var sectionEndIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                sectionEndIndex[currentSection] = i;
                continue;
            }
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                sectionEndIndex[currentSection] = i;
                continue;
            }
            int eq = raw.IndexOf('=');
            if (eq <= 0) continue;
            string key = raw[..eq].Trim();
            var lookup = (currentSection, key);
            if (remaining.TryGetValue(lookup, out var newValue))
            {
                lines[i] = ReplaceValue(raw, eq, newValue);
                remaining.Remove(lookup);
            }
            sectionEndIndex[currentSection] = i;
        }

        // Append remaining keys, grouped by section, after the last line of each section.
        foreach (var sectionGroup in remaining.GroupBy(kv => kv.Key.Item1, StringComparer.OrdinalIgnoreCase))
        {
            var section = sectionGroup.Key;
            if (!sectionEndIndex.TryGetValue(section, out int insertAfter))
            {
                // Section doesn't exist — append at end.
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add("");
                lines.Add($"[{section}]");
                foreach (var kv in sectionGroup) lines.Add($"{kv.Key.Item2} = {kv.Value}");
            }
            else
            {
                // Insert after last line of the section, in reverse so indices stay valid.
                var entries = sectionGroup.Reverse().ToList();
                foreach (var kv in entries)
                    lines.Insert(insertAfter + 1, $"{kv.Key.Item2} = {kv.Value}");
            }
        }

        File.WriteAllLines(path, lines);
    }

    private static string ReplaceValue(string original, int eqIndex, string newValue)
    {
        // Preserve any trailing comment after a ';' on the same line.
        string after = original[(eqIndex + 1)..];
        int commentStart = after.IndexOf(';');
        string trailingComment = commentStart >= 0 ? after[commentStart..] : "";
        // Preserve the original leading whitespace before '=' as-is.
        string head = original[..(eqIndex + 1)];
        string sep = head.EndsWith("= ", StringComparison.Ordinal) ? "" : " ";
        return head + sep + newValue + (trailingComment.Length > 0 ? "  " + trailingComment : "");
    }

    private sealed class KeyComparer : IEqualityComparer<(string Section, string Key)>
    {
        public static readonly KeyComparer Instance = new();
        public bool Equals((string Section, string Key) x, (string Section, string Key) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Section, y.Section)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Key, y.Key);
        public int GetHashCode((string Section, string Key) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Section),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key));
    }
}
