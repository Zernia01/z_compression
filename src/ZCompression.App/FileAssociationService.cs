using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZCompression.App;

internal static class FileAssociationService
{
    internal const string RegisteredApplicationName = "z_compression";
    private const string ProgId = "ZCompression.Archive";
    private const string CapabilitiesPath = @"Software\ZCompression\Capabilities";

    internal static readonly string[] SupportedExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst"];

    internal static void RegisterCurrentUser(string executablePath)
    {
        var executable = Path.GetFullPath(executablePath);
        var openCommand = $"\"{executable}\" \"%1\"";

        SetDefaultValue($@"Software\Classes\{ProgId}", "z_compression archive");
        SetDefaultValue($@"Software\Classes\{ProgId}\DefaultIcon", $"\"{executable}\",0");
        SetDefaultValue($@"Software\Classes\{ProgId}\shell\open\command", openCommand);

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
            capabilities.SetValue("ApplicationDescription", "빠르고 안전한 Windows 압축 및 압축 해제 프로그램", RegistryValueKind.String);
        }
        using (var associations = Registry.CurrentUser.CreateSubKey($@"{CapabilitiesPath}\FileAssociations"))
            foreach (var extension in SupportedExtensions)
                associations.SetValue(extension, ProgId, RegistryValueKind.String);
        using (var registeredApplications = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registeredApplications.SetValue(RegisteredApplicationName, CapabilitiesPath, RegistryValueKind.String);

        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
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
