using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class FileContentionTests
{
    [TestMethod]
    public async Task RecompressingFolderIntoExistingArchiveDoesNotReadItsOwnOutput()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "data.txt");
            await File.WriteAllTextAsync(source, "before");
            var archive = Path.Combine(root, "result.zip");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([root], archive, ArchiveFormat.Zip));
            await File.WriteAllTextAsync(source, "after");
            await engine.CompressAsync(new CompressionRequest([root], archive, ArchiveFormat.Zip));
            await engine.TestAsync(archive);
            var entries = await engine.ListAsync(archive);
            Assert.HasCount(1, entries.Where(entry => !entry.IsDirectory).ToArray());
            Assert.IsFalse(entries.Any(entry => entry.Name == "result.zip"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TemporaryFileLockIsRetriedForCompressionAndExtraction(bool extract)
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "data.txt");
            await File.WriteAllTextAsync(source, "locked content");
            var archive = Path.Combine(root, "result.zip");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Zip));
            var output = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;
            var target = extract ? Path.Combine(output, "data.txt") : source;
            if (extract) await File.WriteAllTextAsync(target, "old");
            using var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);
            var release = Task.Run(async () => { await Task.Delay(350); locked.Dispose(); });
            if (extract) await engine.ExtractAsync(new ExtractionRequest(archive, output));
            else await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Zip));
            await release;
            if (extract) Assert.AreEqual("locked content", await File.ReadAllTextAsync(target));
            else await engine.TestAsync(archive);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CancelWhileDestinationLockedPreservesExistingArchive()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "data.txt");
            await File.WriteAllTextAsync(source, "old data");
            var archive = Path.Combine(root, "result.zip");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Zip));
            var original = await File.ReadAllBytesAsync(archive);
            await File.WriteAllTextAsync(source, "new data");
            using (var locked = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var cancellation = new CancellationTokenSource(350))
            {
                try { await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Zip), cancellationToken: cancellation.Token); Assert.Fail("Cancellation was ignored."); }
                catch (OperationCanceledException) { }
            }
            CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(archive));
            Assert.IsEmpty(Directory.GetFiles(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-contention-" + Guid.NewGuid().ToString("N"))).FullName;
}
