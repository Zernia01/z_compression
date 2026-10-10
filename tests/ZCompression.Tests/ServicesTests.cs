using System.Text;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;
using ZCompression.Core.Collections;
using ZCompression.Update;

namespace ZCompression.Tests;

[TestClass]
public sealed class ServicesTests
{
    [TestMethod]
    public async Task Settings_RoundTripAndRecoverFromCorruption()
    {
        var root = Path.Combine(Path.GetTempPath(), "z_compression-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json"); var service = new JsonSettingsService(path);
            await service.SaveAsync(new AppSettings { Language = "ja-JP", Theme = "dark", CpuThreads = 4, OperationPriority = "high", CheckForUpdatesAtStartup = false, CompressShortcut = "Ctrl+Shift+C", ExtractShortcut = "F8", DefaultCompressionLevel = "fastest", DefaultArchiveFormat = "7z" });
            var loaded = await service.LoadAsync(); Assert.AreEqual("ja-JP", loaded.Language); Assert.AreEqual("dark", loaded.Theme); Assert.AreEqual(4, loaded.CpuThreads); Assert.AreEqual("high", loaded.OperationPriority); Assert.IsFalse(loaded.CheckForUpdatesAtStartup); Assert.AreEqual("Ctrl+Shift+C", loaded.CompressShortcut); Assert.AreEqual("F8", loaded.ExtractShortcut);
            Assert.AreEqual(ZCompression.Core.Archives.CompressionPreset.Fastest, loaded.GetDefaultCompressionLevel());
            Assert.AreEqual(ZCompression.Core.Archives.ArchiveFormat.SevenZip, loaded.GetDefaultArchiveFormat());
            await File.WriteAllTextAsync(path, "{broken", Encoding.UTF8); Assert.AreEqual("auto", (await service.LoadAsync()).Language);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("Store", 0)] [DataRow("FASTEST", 1)] [DataRow("fast", 2)]
    [DataRow("normal", 3)] [DataRow("high", 4)] [DataRow("ultra", 5)]
    [DataRow("99", 4)] [DataRow("unknown", 4)]
    public void DefaultCompressionLevelSupportsAllLevelsAndSafeFallback(string value, int expected) =>
        Assert.AreEqual(expected, (int)new AppSettings { DefaultCompressionLevel = value }.GetDefaultCompressionLevel());

    [TestMethod]
    public async Task ExistingSettingsWithoutCompressionDefaultsKeepPreviousHighLevel()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-old-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            await File.WriteAllTextAsync(path, "{\"Language\":\"ko-KR\"}");
            var settings = await new JsonSettingsService(path).LoadAsync();
            Assert.AreEqual(ZCompression.Core.Archives.CompressionPreset.High, settings.GetDefaultCompressionLevel());
            Assert.AreEqual(ZCompression.Core.Archives.ArchiveFormat.Zip, settings.GetDefaultArchiveFormat());
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void Localization_FallsBackToEnglishKey()
    {
        var root = Path.Combine(Path.GetTempPath(), "z_compression-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "en-US.json"), "{\"OnlyEnglish\":\"fallback\"}"); File.WriteAllText(Path.Combine(root, "ko-KR.json"), "{}");
            var service = new JsonLocalizationService(root, "ko-KR"); Assert.AreEqual("fallback", service["OnlyEnglish"]); Assert.AreEqual("Missing", service["Missing"]);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task Sha256_IsComputedExactly()
    {
        var path = Path.GetTempFileName();
        try { await File.WriteAllTextAsync(path, "abc", new UTF8Encoding(false)); Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", await GitHubUpdateService.ComputeSha256Async(path)); }
        finally { File.Delete(path); }
    }

    [TestMethod]
    [DataRow("1.0.0", "1.0.1", true)] [DataRow("1.10.0", "1.2.0", false)] [DataRow("2.0.0", "2.0.0", false)]
    public void VersionComparison_UsesComponents(string current, string latest, bool expected) => Assert.AreEqual(expected, Version.Parse(latest) > Version.Parse(current));

    [TestMethod]
    public void Rar_SupportsReadingCreationEncryptionAndModification()
    {
        var engine = new ZCompression.Core.Archives.SharpCompressArchiveEngine();
        Assert.IsTrue(engine.Capabilities[ZCompression.Core.Archives.ArchiveFormat.Rar].CanRead);
        Assert.IsTrue(engine.Capabilities[ZCompression.Core.Archives.ArchiveFormat.Rar].CanWrite);
        Assert.IsTrue(engine.Capabilities[ZCompression.Core.Archives.ArchiveFormat.Rar].CanEncrypt);
        Assert.IsTrue(engine.Capabilities[ZCompression.Core.Archives.ArchiveFormat.Rar].CanModify);
        Assert.IsTrue(engine.Capabilities[ZCompression.Core.Archives.ArchiveFormat.Zip].CanModify);
    }

    [TestMethod]
    public void BulkCollection_ReplaceAllRaisesOneReset()
    {
        var collection = new BulkObservableCollection<int>();
        var notifications = 0;
        collection.CollectionChanged += (_, eventArgs) =>
        {
            notifications++;
            Assert.AreEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, eventArgs.Action);
        };
        collection.ReplaceAll(Enumerable.Range(0, 100_000));
        Assert.HasCount(100_000, collection);
        Assert.AreEqual(1, notifications);
    }
}
