namespace ZCompression.Core.Settings;

public static class ExplorerMenuShortcut
{
    // Static Explorer verbs support character mnemonics, not application key gestures.
    public static string? GetAccessKey(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return null;
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrWhiteSpace)) return null;
        if (parts[..^1].Any(part => !new[] { "Ctrl", "Alt", "Shift" }.Contains(part, StringComparer.OrdinalIgnoreCase))) return null;
        var key = parts[^1].ToUpperInvariant();
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z') return key;
        if (key.Length == 2 && key[0] == 'D' && key[1] is >= '0' and <= '9') return key[1..];
        if (key.Length == 7 && key.StartsWith("NUMPAD", StringComparison.Ordinal) && key[6] is >= '0' and <= '9') return key[6..];
        return null;
    }

    public static string BuildLabel(string label, string? shortcut)
    {
        var text = label.Replace("&", "&&", StringComparison.Ordinal);
        return GetAccessKey(shortcut) is { } key ? $"{text} (&{key})" : text;
    }
}
