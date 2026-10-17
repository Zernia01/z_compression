using ZCompression.Update;

namespace ZCompression.Tests;

[TestClass]
public sealed class UpdateInstallerTests
{
    [TestMethod]
    public async Task TemporaryExecutableLockIsRetriedWithoutLosingOtherFiles()
    {
        var root = Root();
        try
        {
            var package = Directory.CreateDirectory(Path.Combine(root, "package")).FullName;
            var install = Directory.CreateDirectory(Path.Combine(root, "install")).FullName;
            await File.WriteAllTextAsync(Path.Combine(package, "app.exe"), "new");
            await File.WriteAllTextAsync(Path.Combine(install, "app.exe"), "old");
            await File.WriteAllTextAsync(Path.Combine(install, "settings.json"), "keep");
            using var locked = new FileStream(Path.Combine(install, "app.exe"), FileMode.Open, FileAccess.Read, FileShare.Read);
            var release = Task.Run(async () => { await Task.Delay(350); locked.Dispose(); });
            await UpdateFileInstaller.ApplyAsync(package, install, "app.exe");
            await release;
            Assert.AreEqual("new", await File.ReadAllTextAsync(Path.Combine(install, "app.exe")));
            Assert.AreEqual("keep", await File.ReadAllTextAsync(Path.Combine(install, "settings.json")));
            Assert.IsEmpty(Directory.GetFiles(install, "*.update-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task FailedInstallRestoresEarlierFilesAndRemovesNewFiles()
    {
        var root = Root();
        try
        {
            var package = Directory.CreateDirectory(Path.Combine(root, "package")).FullName;
            var install = Directory.CreateDirectory(Path.Combine(root, "install")).FullName;
            await File.WriteAllTextAsync(Path.Combine(package, "a-app.exe"), "new");
            await File.WriteAllTextAsync(Path.Combine(package, "b-new.txt"), "new file");
            await File.WriteAllTextAsync(Path.Combine(package, "z-conflict"), "cannot replace a directory");
            await File.WriteAllTextAsync(Path.Combine(install, "a-app.exe"), "old");
            Directory.CreateDirectory(Path.Combine(install, "z-conflict"));
            try { await UpdateFileInstaller.ApplyAsync(package, install, "a-app.exe"); Assert.Fail("Directory conflict was ignored."); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Assert.AreEqual("old", await File.ReadAllTextAsync(Path.Combine(install, "a-app.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(install, "b-new.txt")));
            Assert.IsEmpty(Directory.GetFiles(install, "*.update-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CancellationRestoresPreviousFiles()
    {
        var root = Root();
        try
        {
            var package = Directory.CreateDirectory(Path.Combine(root, "package")).FullName;
            var install = Directory.CreateDirectory(Path.Combine(root, "install")).FullName;
            foreach (var name in new[] { "a-app.exe", "z-locked.txt" })
            {
                await File.WriteAllTextAsync(Path.Combine(package, name), "new");
                await File.WriteAllTextAsync(Path.Combine(install, name), "old");
            }
            using (var locked = new FileStream(Path.Combine(install, "z-locked.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var cancellation = new CancellationTokenSource(350))
            {
                try { await UpdateFileInstaller.ApplyAsync(package, install, "a-app.exe", cancellation.Token); Assert.Fail("Cancellation was ignored."); }
                catch (OperationCanceledException) { }
            }
            Assert.AreEqual("old", await File.ReadAllTextAsync(Path.Combine(install, "a-app.exe")));
            Assert.AreEqual("old", await File.ReadAllTextAsync(Path.Combine(install, "z-locked.txt")));
            Assert.IsEmpty(Directory.GetFiles(install, "*.update-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string Root() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-install-test-" + Guid.NewGuid().ToString("N"))).FullName;
}
