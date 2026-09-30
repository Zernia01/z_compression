using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace ZCompression.App;

internal static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase) ||
            (theme.Equals("system", StringComparison.OrdinalIgnoreCase) && IsSystemDark());
        Set("WindowBackgroundBrush", dark ? "#0B0E13" : "#F5F6FA");
        Set("WorkspaceBackgroundBrush", dark ? "#0B0E13" : "#F5F6FA");
        Set("SidebarBackgroundBrush", dark ? "#10141C" : "#EEF1F6");
        Set("CardBackgroundBrush", dark ? "#11161E" : "#FFFFFF");
        Set("ElevatedBackgroundBrush", dark ? "#171C26" : "#E8ECF3");
        Set("ControlBackgroundBrush", dark ? "#141A24" : "#F8F9FC");
        Set("BorderBrush", dark ? "#27303D" : "#D7DCE5");
        Set("PrimaryTextBrush", dark ? "#F5F7FA" : "#202124");
        Set("SecondaryTextBrush", dark ? "#B7BDC8" : "#667085");
        Set("DisabledTextBrush", dark ? "#596273" : "#9BA3B1");
        Set("AccentSoftBrush", dark ? "#2C294C" : "#E6E2FF");
    }

    private static bool IsSystemDark()
    {
        var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
        return value is int number && number == 0;
    }

    private static void Set(string key, string color) => Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
