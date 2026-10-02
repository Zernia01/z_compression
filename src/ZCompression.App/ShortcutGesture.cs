using System.Windows.Input;

namespace ZCompression.App;

internal static class ShortcutGesture
{
    private const ModifierKeys SupportedModifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

    public static bool TryCreate(Key key, ModifierKeys modifiers, out string shortcut)
    {
        shortcut = string.Empty;
        modifiers &= SupportedModifiers;
        if (IsModifierKey(key) || key is Key.None or Key.Escape or Key.Tab) return false;
        if (modifiers == ModifierKeys.None && (key < Key.F1 || key > Key.F24)) return false;
        shortcut = Format(key, modifiers);
        return true;
    }

    public static bool Matches(string shortcut, Key key, ModifierKeys modifiers)
    {
        if (!TryParse(shortcut, out var configuredKey, out var configuredModifiers)) return false;
        return configuredKey == key && configuredModifiers == (modifiers & SupportedModifiers);
    }

    public static bool TryParse(string shortcut, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !Enum.TryParse(parts[^1], true, out key) || IsModifierKey(key)) return false;
        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
            else return false;
        }
        return modifiers != ModifierKeys.None || key is >= Key.F1 and <= Key.F24;
    }

    private static string Format(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join('+', parts);
    }

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}
