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
        var registration = CreateRegistration();
        var openCommand = $"\"{executable}\" \"%1\"";

        registration.SetDefaultValue($@"Software\Classes\{ProgId}", "z_compression archive");
        registration.SetDefaultValue($@"Software\Classes\{ProgId}\DefaultIcon", $"\"{executable}\",0");
        registration.SetDefaultValue($@"Software\Classes\{ProgId}\shell\open\command", openCommand);

        foreach (var extension in new[] { ".zip", ".7z", ".rar" })
        {
            var formatProgId = GetProgId(extension);
            var iconPath = Path.Combine(Path.GetDirectoryName(executable)!, "Assets", "FileTypes", extension[1..] + ".ico");
            registration.SetDefaultValue($@"Software\Classes\{formatProgId}", $"z_compression {extension[1..].ToUpperInvariant()} archive");
            registration.SetDefaultValue($@"Software\Classes\{formatProgId}\DefaultIcon", $"\"{iconPath}\",0");
            registration.SetDefaultValue($@"Software\Classes\{formatProgId}\shell\open\command", openCommand);
            using var openWith = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");
            registration.SetValue(openWith, formatProgId, Array.Empty<byte>(), RegistryValueKind.None);
            // Migrate our unprotected legacy association without replacing another app's choice.
            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            if (extensionKey.GetValue(null) is string previous && previous == ProgId)
                registration.SetValue(extensionKey, "", formatProgId, RegistryValueKind.String);
        }

        var applicationPath = $@"Software\Classes\Applications\{Path.GetFileName(executable)}";
        using (var application = Registry.CurrentUser.CreateSubKey(applicationPath))
            registration.SetValue(application, "FriendlyAppName", RegisteredApplicationName, RegistryValueKind.String);
        registration.SetDefaultValue($@"{applicationPath}\DefaultIcon", $"\"{executable}\",0");
        registration.SetDefaultValue($@"{applicationPath}\shell\open\command", openCommand);
        using (var supportedTypes = Registry.CurrentUser.CreateSubKey($@"{applicationPath}\SupportedTypes"))
            foreach (var extension in SupportedExtensions)
                registration.SetValue(supportedTypes, extension, string.Empty, RegistryValueKind.String);

        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesPath))
        {
            registration.SetValue(capabilities, "ApplicationName", RegisteredApplicationName, RegistryValueKind.String);
            registration.SetValue(capabilities, "ApplicationDescription", LocalizationManager.Instance["Subtitle"], RegistryValueKind.String);
        }
        using (var associations = Registry.CurrentUser.CreateSubKey($@"{CapabilitiesPath}\FileAssociations"))
            foreach (var extension in SupportedExtensions)
                registration.SetValue(associations, extension, GetProgId(extension), RegistryValueKind.String);
        using (var registeredApplications = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registration.SetValue(registeredApplications, RegisteredApplicationName, CapabilitiesPath, RegistryValueKind.String);

        RegisterContextMenus(executable, settings, registration);
        registration.NotifyIfChanged();
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
        var registration = CreateRegistration();
        RegisterContextMenus(Path.GetFullPath(executablePath), settings, registration);
        registration.NotifyIfChanged();
    }

    private static void RegisterContextMenus(string executable, AppSettings settings, ShellRegistrationBatch registration)
    {
        foreach (var itemType in new[] { "*", "Directory" })
            RegisterMenu(registration, $@"Software\Classes\{itemType}\shell\ZCompression.Compress", ExplorerMenuShortcut.BuildLabel(LocalizationManager.Instance["ShellCompressNow"], settings.CompressShortcut), "--compress-here", executable);
        // SystemFileAssociations keeps extraction available when another app is the default.
        foreach (var extension in SupportedExtensions)
            RegisterMenu(registration, $@"Software\Classes\SystemFileAssociations\{extension}\shell\ZCompression.Extract", ExplorerMenuShortcut.BuildLabel(LocalizationManager.Instance["ShellExtractNow"], settings.ExtractShortcut), "--extract-here", executable);
    }

    private static void RegisterMenu(ShellRegistrationBatch registration, string path, string label, string argument, string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        registration.SetValue(key, "MUIVerb", label, RegistryValueKind.String);
        registration.SetValue(key, "Icon", $"\"{executable}\",0", RegistryValueKind.String);
        registration.SetValue(key, "MultiSelectModel", "Single", RegistryValueKind.String);
        registration.SetDefaultValue($@"{path}\command", $"\"{executable}\" {argument} \"%1\"");
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

    private static ShellRegistrationBatch CreateRegistration() =>
        new(() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero));

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
