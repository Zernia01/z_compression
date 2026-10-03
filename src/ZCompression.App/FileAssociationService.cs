using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ZCompression.Core.Settings;

namespace ZCompression.App;

internal static class FileAssociationService
{
    internal const string RegisteredApplicationName = "z_compression";
    private const string ProgId = "ZCompression.Archive";
    private const string CapabilitiesPath = @"Software\ZCompression\Capabilities";

    internal static readonly string[] SupportedExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst"];

    internal static void RegisterCurrentUser(string executablePath, AppSettings settings)
    {
        var executable = Path.GetFullPath(executablePath);
        var openCommand = $"\"{executable}\" \"%1\"";

        SetDefaultValue($@"Software\Classes\{ProgId}", "z_compression archive");
        SetDefaultValue($@"Software\Classes\{ProgId}\DefaultIcon", $"\"{executable}\",0");
        SetDefaultValue($@"Software\Classes\{ProgId}\shell\open\command", openCommand);

        foreach (var extension in new[] { ".zip", ".7z", ".rar" })
        {
            var formatProgId = GetProgId(extension);
            var iconPath = Path.Combine(Path.GetDirectoryName(executable)!, "Assets", "FileTypes", extension[1..] + ".ico");
            SetDefaultValue($@"Software\Classes\{formatProgId}", $"z_compression {extension[1..].ToUpperInvariant()} archive");
            SetDefaultValue($@"Software\Classes\{formatProgId}\DefaultIcon", $"\"{iconPath}\",0");
            SetDefaultValue($@"Software\Classes\{formatProgId}\shell\open\command", openCommand);
            using var openWith = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
            openWith.SetValue(formatProgId, Array.Empty<byte>(), RegistryValueKind.None);
            // Migrate our unprotected legacy association without replacing another app's choice.
            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            if (extensionKey.GetValue(null) is string previous && previous == ProgId)
                extensionKey.SetValue(null, formatProgId, RegistryValueKind.String);
        }

        var applicationPath = $@"Software\Classes\Applications\{Path.GetFileName(executable)}";
        using (var application = Registry.CurrentUser.CreateSubKey(applicationPath))
            application.SetValue("FriendlyAppName", RegisteredApplicationName, RegistryValueKind.String);
        SetDefaultValue($@"{applicationPath}\DefaultIcon", $"\"{executable}\",0");
        SetDefaultValue($@"{applicationPath}\shell\open\command", openCommand);
        using (var supportedTypes = Registry.CurrentUser.CreateSubKey($@"{applicationPath}\SupportedTypes"))
            foreach (var extension in SupportedExtensions)
                supportedTypes.SetValue(extension, string.Empty, RegistryValueKind.String);

        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesPath))
        {
            capabilities.SetValue("ApplicationName", RegisteredApplicationName, RegistryValueKind.String);
            capabilities.SetValue("ApplicationDescription", LocalizationManager.Instance["Subtitle"], RegistryValueKind.String);
        }
        using (var associations = Registry.CurrentUser.CreateSubKey($@"{CapabilitiesPath}\FileAssociations"))
            foreach (var extension in SupportedExtensions)
                associations.SetValue(extension, GetProgId(extension), RegistryValueKind.String);
        using (var registeredApplications = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registeredApplications.SetValue(RegisteredApplicationName, CapabilitiesPath, RegistryValueKind.String);

        RegisterContextMenus(executable, settings);

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static string GetProgId(string extension) => extension switch
    {
        ".zip" => "ZCompression.Zip",
        ".7z" => "ZCompression.SevenZip",
        ".rar" => "ZCompression.Rar",
        _ => ProgId
    };

    internal static void RegisterContextMenus(string executablePath, AppSettings settings)
    {
        var executable = Path.GetFullPath(executablePath);
        foreach (var itemType in new[] { "*", "Directory" })
            RegisterMenu($@"Software\Classes\{itemType}\shell\ZCompression.Compress", ExplorerMenuShortcut.BuildLabel(LocalizationManager.Instance["ShellCompressNow"], settings.CompressShortcut), "--compress-here", executable);
        // SystemFileAssociations keeps extraction available when another app is the default.
        foreach (var extension in SupportedExtensions)
            RegisterMenu($@"Software\Classes\SystemFileAssociations\{extension}\shell\ZCompression.Extract", ExplorerMenuShortcut.BuildLabel(LocalizationManager.Instance["ShellExtractNow"], settings.ExtractShortcut), "--extract-here", executable);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RegisterMenu(string path, string label, string argument, string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue("MUIVerb", label, RegistryValueKind.String);
        key.SetValue("Icon", $"\"{executable}\",0", RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
        SetDefaultValue($@"{path}\command", $"\"{executable}\" {argument} \"%1\"");
    }

    internal static void OpenDefaultAppsSettings()
    {
        var targetedUri = $"ms-settings:defaultapps?registeredAppUser={Uri.EscapeDataString(RegisteredApplicationName)}";
        try
        {
            Process.Start(new ProcessStartInfo(targetedUri) { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
    }

    private static void SetDefaultValue(string path, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue(null, value, RegistryValueKind.String);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
