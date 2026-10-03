using System.Text;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class QuickArchiveTests
{
    [TestMethod]
    [DataRow(ArchiveFormat.Zip)]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.TarGZip)]
    public async Task QuickOperations_CreateBesideSourceAndPreserveExistingResults(ArchiveFormat format)
    {
        var root = TemporaryDirectory();
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "한글 folder")).FullName;
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            var content = "quick action 日本語 😀";
            await File.WriteAllTextAsync(Path.Combine(source, "data.txt"), content, Encoding.UTF8);
            var service = new QuickArchiveService(new SharpCompressArchiveEngine());
            var first = await service.CompressAsync([source], format);
            Assert.AreEqual(root, Path.GetDirectoryName(first));
            var engine = new SharpCompressArchiveEngine();
            await engine.TestAsync(first);
            var entry = (await engine.ListAsync(first)).Single(item => item.Path.EndsWith("data.txt", StringComparison.Ordinal));
            var preview = Path.Combine(root, "preview.txt");
            await engine.ExtractEntryAsync(first, entry.Path, preview);
            Assert.AreEqual(content, await File.ReadAllTextAsync(preview));
            var bytes = await File.ReadAllBytesAsync(first);
            var second = await service.CompressAsync([source], format);
            StringAssert.Contains(Path.GetFileName(second), " (2)");
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(first));
            var output = await service.ExtractAsync(first);
            Assert.AreEqual(Path.Combine(root, "한글 folder (2)"), output, "Original source folder must be preserved.");
            Assert.AreEqual(content, await File.ReadAllTextAsync(Path.Combine(output, "한글 folder", "data.txt")));
            Assert.IsTrue(Directory.Exists(Path.Combine(output, "한글 folder", "empty")));
            var nextOutput = await service.ExtractAsync(first);
            Assert.AreEqual(Path.Combine(root, "한글 folder (3)"), nextOutput);
            Assert.AreEqual(content, await File.ReadAllTextAsync(Path.Combine(source, "data.txt")));
            Assert.IsEmpty(Directory.GetFileSystemEntries(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task QuickCompression_ConcurrentActionsProduceSeparateArchives()
    {
        var root = TemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "same.txt");
            await File.WriteAllTextAsync(source, "concurrent content");
            var service = new QuickArchiveService(new SharpCompressArchiveEngine());
            var results = await Task.WhenAll(service.CompressAsync([source]), service.CompressAsync([source]));
            Assert.AreNotEqual(results[0], results[1]);
            foreach (var archive in results) await new SharpCompressArchiveEngine().TestAsync(archive);
            Assert.IsEmpty(Directory.GetFileSystemEntries(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task QuickExtraction_FailedPasswordLeavesNoPartialResultAndCanBeRetried()
    {
        var root = TemporaryDirectory();
        try
        {
            var archive = Path.Combine(root, "encrypted.rar");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.encrypted_filesOnly.rar"), archive);
            var service = new QuickArchiveService(new SharpCompressArchiveEngine());
            await Assert.ThrowsExactlyAsync<SharpCompress.Common.CryptographicException>(() => service.ExtractAsync(archive, "incorrect"));
            Assert.IsEmpty(Directory.GetDirectories(root));
            var output = await service.ExtractAsync(archive, "test");
            Assert.AreEqual(Path.Combine(root, "encrypted"), output);
            Assert.IsNotEmpty(Directory.GetFiles(output, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task QuickExtraction_CancellationPreservesExistingFolder()
    {
        var root = TemporaryDirectory();
        try
        {
            var archive = Path.Combine(root, "existing.rar");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.rar"), archive);
            var output = Directory.CreateDirectory(Path.Combine(root, "existing")).FullName;
            var sentinel = Path.Combine(output, "keep.txt");
            await File.WriteAllTextAsync(sentinel, "keep");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => new QuickArchiveService(new SharpCompressArchiveEngine()).ExtractAsync(archive, cancellationToken: cancellation.Token));
            Assert.AreEqual("keep", await File.ReadAllTextAsync(sentinel));
            Assert.IsEmpty(Directory.GetFileSystemEntries(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string TemporaryDirectory() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-compression-quick-tests", Guid.NewGuid().ToString("N"))).FullName;
}
