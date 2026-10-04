using Microsoft.Win32;
using System.Runtime.Versioning;
using ZCompression.App;

namespace ZCompression.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class ShellRegistrationBatchTests
{
    [TestMethod]
    public void RepeatedLaunchWithIdenticalRegistration_DoesNotWriteOrRefreshExplorer()
    {
        var writes = 0;
        var notifications = 0;
        var batch = new ShellRegistrationBatch(() => notifications++);
        batch.SetValue("icon.ico,0", RegistryValueKind.String, "icon.ico,0", RegistryValueKind.String, () => writes++);
        batch.SetValue(Array.Empty<byte>(), RegistryValueKind.None, new byte[0], RegistryValueKind.None, () => writes++);
        batch.NotifyIfChanged();
        Assert.AreEqual(0, writes);
        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public void ChangedShortcutAndIcon_RefreshExplorerOnceAfterRegistration()
    {
        var writes = 0;
        var notifications = 0;
        var batch = new ShellRegistrationBatch(() => notifications++);
        batch.SetValue("Compress (&U)", RegistryValueKind.String, "Compress (&K)", RegistryValueKind.String, () => writes++);
        batch.SetValue("old.ico,0", RegistryValueKind.String, "zip.ico,0", RegistryValueKind.String, () => writes++);
        Assert.AreEqual(0, notifications);
        batch.NotifyIfChanged();
        batch.NotifyIfChanged();
        Assert.AreEqual(2, writes);
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public void MissingEmptyValueOrWrongRegistryType_IsRepairedAndNotified()
    {
        var writes = 0;
        var notifications = 0;
        var batch = new ShellRegistrationBatch(() => notifications++);
        batch.SetValue(null, null, "", RegistryValueKind.String, () => writes++);
        batch.SetValue("value", RegistryValueKind.ExpandString, "value", RegistryValueKind.String, () => writes++);
        batch.NotifyIfChanged();
        Assert.AreEqual(2, writes);
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public void RegistryRoundTrip_SecondRegistrationDoesNotRefresh()
    {
        var path = @"Software\ZCompression.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            var notifications = 0;
            void Register()
            {
                var batch = new ShellRegistrationBatch(() => notifications++);
                batch.SetValue(key, "", "archive", RegistryValueKind.String);
                batch.SetValue(key, "OpenWith", Array.Empty<byte>(), RegistryValueKind.None);
                batch.SetValue(key, "SupportedType", "", RegistryValueKind.String);
                batch.NotifyIfChanged();
            }
            Register();
            Assert.AreEqual(1, notifications);
            Register();
            Assert.AreEqual(1, notifications);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false); }
    }
}
