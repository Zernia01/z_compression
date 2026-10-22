using System.Buffers.Binary;
using System.IO.Compression;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class ZipVerificationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerificationDetectsStoredZipPayloadCorruption(bool corrupt)
    {
        var root = Root();
        try
        {
            var path = Path.Combine(root, "archive.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var entry = zip.CreateEntry("日本語.txt", CompressionLevel.NoCompression).Open()) entry.Write(new byte[8192]);
            if (corrupt)
            {
                var bytes = File.ReadAllBytes(path);
                var offset = 30 + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26)) + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
                bytes[offset] ^= 0x7f;
                File.WriteAllBytes(path, bytes);
                var exception = Assert.ThrowsExactly<InvalidDataException>(() => ZipIntegrityVerifier.Verify(path));
                StringAssert.Contains(exception.Message, "CRC");
            }
            else ZipIntegrityVerifier.Verify(path, expectedCount: 1, expectedBytes: 8192);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CancelVerificationKeepsThePreviousArchive()
    {
        var root = Root();
        try
        {
            var source = Path.Combine(root, "source.txt");
            var destination = Path.Combine(root, "archive.zip");
            await File.WriteAllTextAsync(source, "original");
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], destination, ArchiveFormat.Zip));
            var original = await File.ReadAllBytesAsync(destination);
            await File.WriteAllTextAsync(source, "replacement");
            using var cancellation = new CancellationTokenSource();
            var progress = new Immediate(value => { if (value.IsVerifying) cancellation.Cancel(); });
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => engine.CompressAsync(new CompressionRequest([source], destination, ArchiveFormat.Zip), progress, cancellation.Token));
            CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(destination));
            Assert.IsEmpty(Directory.GetFiles(root, ".z-compression-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CompletionIsOnlyReportedAfterVerificationAndPublication()
    {
        var root = Root();
        try
        {
            var source = Path.Combine(root, "source.txt");
            await File.WriteAllTextAsync(source, "completed");
            var destination = Path.Combine(root, "archive.zip");
            var verified = false; var completed = false;
            var progress = new Immediate(value =>
            {
                if (value.IsVerifying) verified = true;
                if (value.Percent < 100) return;
                Assert.IsTrue(verified);
                Assert.IsTrue(File.Exists(destination));
                ZipIntegrityVerifier.Verify(destination);
                completed = true;
            });
            await new SharpCompressArchiveEngine().CompressAsync(new CompressionRequest([source], destination, ArchiveFormat.Zip), progress);
            Assert.IsTrue(completed);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Immediate(Action<ArchiveProgress> report) : IProgress<ArchiveProgress> { public void Report(ArchiveProgress value) => report(value); }
    private static string Root() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-zip-check-" + Guid.NewGuid().ToString("N"))).FullName;
}
