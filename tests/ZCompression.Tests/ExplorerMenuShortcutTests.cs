using ZCompression.Core.Settings;

namespace ZCompression.Tests;

[TestClass]
public sealed class ExplorerMenuShortcutTests
{
    [TestMethod]
    [DataRow("U", "압축 (&U)")]
    [DataRow("u", "압축 (&U)")]
    [DataRow("Ctrl+E", "압축 (&E)")]
    [DataRow("Ctrl+Shift+K", "압축 (&K)")]
    [DataRow("D7", "압축 (&7)")]
    [DataRow("NumPad3", "압축 (&3)")]
    [DataRow("", "압축")]
    [DataRow("F8", "압축")]
    [DataRow("OemMinus", "압축")]
    [DataRow("Space", "압축")]
    [DataRow("Ctrl+", "압축")]
    public void MenuLabels_FollowConfiguredShortcutAndRemoveUnsupportedAccelerators(string shortcut, string expected) =>
        Assert.AreEqual(expected, ExplorerMenuShortcut.BuildLabel("압축", shortcut));

    [TestMethod]
    public async Task SavedShortcuts_ReloadIntoMenuWithoutFixedZOrUKeys()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-compression-menu-tests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var service = new JsonSettingsService(Path.Combine(root, "settings.json"));
            await service.SaveAsync(new AppSettings { CompressShortcut = "U", ExtractShortcut = "Ctrl+E" });
            var saved = await service.LoadAsync();
            Assert.AreEqual("Compress (&U)", ExplorerMenuShortcut.BuildLabel("Compress", saved.CompressShortcut));
            Assert.AreEqual("Extract (&E)", ExplorerMenuShortcut.BuildLabel("Extract", saved.ExtractShortcut));
            await service.SaveAsync(saved with { CompressShortcut = "K", ExtractShortcut = "" });
            var changed = await service.LoadAsync();
            Assert.AreEqual("Compress (&K)", ExplorerMenuShortcut.BuildLabel("Compress", changed.CompressShortcut));
            Assert.AreEqual("Extract", ExplorerMenuShortcut.BuildLabel("Extract", changed.ExtractShortcut));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void MenuLabel_LiteralAmpersandsDoNotCreateUnexpectedAccelerators() =>
        Assert.AreEqual("Files && folders (&J)", ExplorerMenuShortcut.BuildLabel("Files & folders", "J"));
}
