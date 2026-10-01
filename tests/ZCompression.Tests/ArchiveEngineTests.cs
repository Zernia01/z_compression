using System.IO.Compression;
using System.Text;
using ZCompression.Core.Archives;
using ZCompression.Core.Security;

namespace ZCompression.Tests;

[TestClass]
public sealed class ArchiveEngineTests
{
    private static readonly string[] UnicodeNames = ["hello.txt", "안녕하세요.txt", "日本語テスト.txt", "简体中文测试.txt", "繁體中文測試.txt", "漢字한글混合.txt", "한글-日本語-中文-😀.txt", "공백 포함 파일.txt", "[특수문자] 테스트.txt"];

    [TestMethod]
    [DataRow(ArchiveFormat.Zip, ".zip")]
    [DataRow(ArchiveFormat.SevenZip, ".7z")]
    public async Task UnicodeFiles_RoundTrip(ArchiveFormat format, string extension)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            foreach (var name in UnicodeNames) await File.WriteAllTextAsync(Path.Combine(source, name), "content:" + name, Encoding.UTF8);
            await File.WriteAllBytesAsync(Path.Combine(source, "empty.bin"), []);
            Directory.CreateDirectory(Path.Combine(source, "empty-folder"));
            var archive = Path.Combine(root, "test" + extension); var destination = Path.Combine(root, "output");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, format));
            await engine.TestAsync(archive);
            var preview = Path.Combine(root, "preview.txt");
            await engine.ExtractEntryAsync(archive, "source/안녕하세요.txt", preview);
            Assert.AreEqual("content:안녕하세요.txt", await File.ReadAllTextAsync(preview, Encoding.UTF8));
            await engine.ExtractAsync(new ExtractionRequest(archive, destination));
            foreach (var name in UnicodeNames)
            {
                var extracted = Path.Combine(destination, "source", name);
                Assert.IsTrue(File.Exists(extracted), name);
                Assert.AreEqual("content:" + name, await File.ReadAllTextAsync(extracted, Encoding.UTF8));
            }
            Assert.AreEqual(0, new FileInfo(Path.Combine(destination, "source", "empty.bin")).Length);
            Assert.IsTrue(Directory.Exists(Path.Combine(destination, "source", "empty-folder")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task Extraction_BlocksZipSlip()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = Path.Combine(root, "malicious.zip");
            await using (var stream = File.Create(archivePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../../escaped.txt"); await using var writer = new StreamWriter(entry.Open()); await writer.WriteAsync("unsafe");
            }
            var engine = new SharpCompressArchiveEngine();
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => engine.ExtractAsync(new ExtractionRequest(archivePath, Path.Combine(root, "out"))));
            Assert.IsFalse(File.Exists(Path.Combine(root, "escaped.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SafePath_BlocksAbsoluteAndTraversalPaths()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => SafeExtractionPath.Resolve(root, "../escape.txt"));
            Assert.ThrowsExactly<InvalidDataException>(() => SafeExtractionPath.Resolve(root, "C:\\Windows\\file.txt"));
            Assert.IsTrue(SafeExtractionPath.Resolve(root, "safe/f.txt").StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task Cancellation_IsObserved()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var file = Path.Combine(root, "file.txt"); await File.WriteAllTextAsync(file, "test"); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => new SharpCompressArchiveEngine().CompressAsync(new CompressionRequest([file], Path.Combine(root, "cancel.zip"), ArchiveFormat.Zip), cancellationToken: cancellation.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task Listing_UnchangedArchiveUsesCachedMetadata()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = Path.Combine(root, "cached.zip");
            await using (var stream = File.Create(archivePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("file.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("cached");
            }
            var engine = new SharpCompressArchiveEngine();
            var first = await engine.ListAsync(archivePath);
            var second = await engine.ListAsync(archivePath);
            Assert.AreSame(first, second);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task HighCompression_IsNoLargerThanFastestForCompressibleData()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "compressible.txt");
            await File.WriteAllTextAsync(source, string.Concat(Enumerable.Repeat("z_compression 압축 효율 검증 데이터 0123456789\n", 50_000)), Encoding.UTF8);
            var fastest = Path.Combine(root, "fastest.zip");
            var high = Path.Combine(root, "high.zip");
            var engine = new SharpCompressArchiveEngine();

            await engine.CompressAsync(new CompressionRequest([source], fastest, ArchiveFormat.Zip, CompressionPreset.Fastest));
            await engine.CompressAsync(new CompressionRequest([source], high, ArchiveFormat.Zip, CompressionPreset.High));

            Assert.IsLessThanOrEqualTo(new FileInfo(fastest).Length, new FileInfo(high).Length, "High compression should not produce a larger ZIP than the fastest preset for compressible data.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateTemporaryDirectory() { var path = Path.Combine(Path.GetTempPath(), "z_compression-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}
