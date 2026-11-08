using System.IO.Compression;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class LargeZipTests
{
    [TestMethod]
    [DataRow(CompressionPreset.Fastest)]
    [DataRow(CompressionPreset.Store)]
    public async Task ZipRoundTripsAnEntryLargerThanFourGiB(CompressionPreset level)
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-large-zip-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            const long length = (1L << 32) + 257;
            var source = Path.Combine(root, "日本語-대용량.bin");
            using (var file = File.Create(source))
            {
                file.SetLength(length);
                file.WriteByte(0x42);
                file.Position = length - 1;
                file.WriteByte(0x7f);
            }
            var tail = Path.Combine(root, "中文-tail.txt");
            await File.WriteAllTextAsync(tail, "after the large entry");
            var destination = Path.Combine(root, "large.zip");
            var engine = new SharpCompressArchiveEngine();
            // CompressAsync also reads every byte back and checks its CRC before publication.
            await engine.CompressAsync(new CompressionRequest([source, tail], destination, ArchiveFormat.Zip, level));
            if (level == CompressionPreset.Store) Assert.IsGreaterThan((long)uint.MaxValue, new FileInfo(destination).Length);
            using (var zip = ZipFile.OpenRead(destination))
            {
                Assert.HasCount(2, zip.Entries);
                Assert.AreEqual(length, zip.GetEntry(Path.GetFileName(source))!.Length);
                using var content = zip.GetEntry(Path.GetFileName(source))!.Open();
                Assert.AreEqual(0x42, content.ReadByte());
                using var reader = new StreamReader(zip.GetEntry(Path.GetFileName(tail))!.Open());
                Assert.AreEqual("after the large entry", await reader.ReadToEndAsync());
            }
            var entries = await engine.ListAsync(destination);
            Assert.AreEqual(length, entries.Single(entry => entry.Path == Path.GetFileName(source)).OriginalSize);
            var extracted = Path.Combine(root, "tail.txt");
            await engine.ExtractEntryAsync(destination, Path.GetFileName(tail), extracted);
            Assert.AreEqual("after the large entry", await File.ReadAllTextAsync(extracted));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ExtractionAndDragSelectionHaveNoDefaultByteCap()
    {
        const long size = 101L * 1024 * 1024 * 1024;
        Assert.AreEqual(long.MaxValue, new ExtractionRequest("large.zip", "output").MaximumExpandedBytes);
        var manifest = ArchiveExportManifest.Create([new ArchiveEntryInfo("large.bin", "large.bin", size, size, null, null, false)], null, "");
        Assert.AreEqual(size, manifest.Single().Size);
    }
}
