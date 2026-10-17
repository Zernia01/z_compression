using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class SplitArchiveTests
{
    [TestMethod]
    public void DifferentPartsAreRecognizedAsTheSameArchive()
    {
        Assert.IsTrue(SplitArchiveService.IsSameArchive("bundle.zip.001", "bundle.zip.002"));
        Assert.IsFalse(SplitArchiveService.IsSameArchive("bundle.zip.001", "other.zip.002"));
        Assert.IsFalse(SplitArchiveService.IsVolumePath("notes.001"));
        Assert.IsFalse(SplitArchiveService.IsVolumePath("bundle.zip.000"));
    }
    [TestMethod]
    [DataRow(ArchiveFormat.Zip, ".zip")]
    [DataRow(ArchiveFormat.SevenZip, ".7z")]
    [DataRow(ArchiveFormat.Tar, ".tar")]
    [DataRow(ArchiveFormat.TarGZip, ".tar.gz")]
    public async Task SplitArchivesRoundTripAndRespectSize(ArchiveFormat format, string extension)
    {
        var root = CreateRoot();
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "한글 폴더")).FullName;
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            var bytes = new byte[240000]; new Random(17).NextBytes(bytes);
            await File.WriteAllBytesAsync(Path.Combine(source, "데이터.bin"), bytes);
            var engine = new SharpCompressArchiveEngine();
            var destination = Path.Combine(root, "bundle" + extension);
            var first = await new SplitArchiveService(engine).CompressAsync(new CompressionRequest([source], destination, format), 65536);
            Assert.AreEqual(destination + ".001", first);
            var parts = Directory.GetFiles(root, "bundle" + extension + ".*");
            Assert.IsGreaterThan(1, parts.Length);
            Assert.IsTrue(parts.All(path => new FileInfo(path).Length <= 65536));
            if (format == ArchiveFormat.TarGZip)
            {
                var joined = Path.Combine(root, "joined.tar.gz");
                await using (var joinedOutput = File.Create(joined))
                    foreach (var part in parts.Order(StringComparer.Ordinal))
                    { await using var input = File.OpenRead(part); await input.CopyToAsync(joinedOutput); }
                await engine.ListAsync(joined);
                File.Delete(joined);
            }
            var entries = await engine.ListAsync(first);
            Assert.IsTrue(entries.Any(entry => entry.Path.EndsWith("데이터.bin", StringComparison.Ordinal)));
            await engine.TestAsync(first);
            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(destination + ".002", output));
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(Path.Combine(output, "한글 폴더", "데이터.bin")));
            Assert.IsTrue(Directory.Exists(Path.Combine(output, "한글 폴더", "empty")));
            Assert.AreEqual("bundle", QuickArchiveService.GetExtractionFolderName(first));
            var preview = Path.Combine(root, "preview.bin");
            await engine.ExtractEntryAsync(first, "한글 폴더/데이터.bin", preview);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(preview));
            var original = await File.ReadAllBytesAsync(first);
            try { await new SplitArchiveService(engine).CompressAsync(new CompressionRequest([source], destination, format), 65536); Assert.Fail("Existing volumes were accepted."); }
            catch (IOException) { }
            CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(first));
            File.Delete(destination + ".002");
            try { await engine.ListAsync(first); Assert.Fail("Missing volume was accepted or stale metadata was returned."); }
            catch (InvalidDataException) { }
            Assert.IsEmpty(Directory.GetDirectories(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CancelSplittingPublishesNoVolumes()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source.bin");
            var bytes = new byte[240000]; new Random(18).NextBytes(bytes);
            await File.WriteAllBytesAsync(source, bytes);
            using var cancellation = new CancellationTokenSource();
            var progress = new ImmediateProgress(value => { if (value.Percent > 80) cancellation.Cancel(); });
            try
            {
                await new SplitArchiveService(new SharpCompressArchiveEngine()).CompressAsync(new CompressionRequest([source], Path.Combine(root, "bundle.zip"), ArchiveFormat.Zip), 65536, progress, cancellation.Token);
                Assert.Fail("Cancellation was ignored.");
            }
            catch (OperationCanceledException) { }
            Assert.HasCount(1, Directory.GetFiles(root));
            Assert.IsEmpty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SplitRarCanBeReadAcrossVolumeBoundaries()
    {
        var root = CreateRoot();
        try
        {
            var original = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.solid.rar");
            var bytes = await File.ReadAllBytesAsync(original);
            var basePath = Path.Combine(root, "rar archive.rar");
            for (var offset = 0; offset < bytes.Length; offset += 512)
                await File.WriteAllBytesAsync(basePath + $".{offset / 512 + 1:D3}", bytes[offset..Math.Min(bytes.Length, offset + 512)]);
            var engine = new SharpCompressArchiveEngine();
            var expected = await engine.ListAsync(original);
            var actual = await engine.ListAsync(basePath + ".001");
            CollectionAssert.AreEqual(expected.Select(entry => entry.Path).ToArray(), actual.Select(entry => entry.Path).ToArray());
            await engine.TestAsync(basePath + ".001");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class ImmediateProgress(Action<ArchiveProgress> report) : IProgress<ArchiveProgress> { public void Report(ArchiveProgress value) => report(value); }
    private static string CreateRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-split-tests-" + Guid.NewGuid().ToString("N"))).FullName;
}
