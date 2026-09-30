using System.Text;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;
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
            await service.SaveAsync(new AppSettings { Language = "ja-JP", Theme = "dark", CpuThreads = 4 });
            var loaded = await service.LoadAsync(); Assert.AreEqual("ja-JP", loaded.Language); Assert.AreEqual("dark", loaded.Theme); Assert.AreEqual(4, loaded.CpuThreads);
            await File.WriteAllTextAsync(path, "{broken", Encoding.UTF8); Assert.AreEqual("auto", (await service.LoadAsync()).Language);
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
}
