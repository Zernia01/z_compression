using ZCompression.Core.Archives;
using ZCompression.Core.Settings;

namespace ZCompression.Tests;

[TestClass]
public sealed class PasswordAndFolderTests
{
    [TestMethod]
    public async Task EncryptedZipVerificationDetectsTamperedPayload()
    {
        var root = Directory.CreateTempSubdirectory("z-aes-integrity-").FullName;
        try
        {
            var input = Path.Combine(root, "file.bin");
            var payload = new byte[65536];
            new Random(5).NextBytes(payload);
            await File.WriteAllBytesAsync(input, payload);
            var archive = Path.Combine(root, "secret.zip");
            await new SharpCompressArchiveEngine().CompressAsync(new CompressionRequest([input], archive, ArchiveFormat.Zip, Password: "secret"));
            var bytes = await File.ReadAllBytesAsync(archive);
            var nameLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26));
            var extraLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
            bytes[30 + nameLength + extraLength + 64] ^= 0x80;
            await File.WriteAllBytesAsync(archive, bytes);
            Assert.ThrowsExactly<ICSharpCode.SharpZipLib.Zip.ZipException>(() => ZipIntegrityVerifier.Verify(archive, password: "secret"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AesZipAndSplitZipRoundTripWithPassword(bool split)
    {
        var root = Directory.CreateTempSubdirectory("z-password-").FullName;
        try
        {
            var input = Path.Combine(root, "日本語-한글.txt");
            var content = new byte[160 * 1024];
            new Random(42).NextBytes(content);
            await File.WriteAllBytesAsync(input, content);
            var engine = new SharpCompressArchiveEngine();
            var request = new CompressionRequest([input], Path.Combine(root, "secret.zip"), ArchiveFormat.Zip, Password: "shared-암호");
            var archive = request.Destination;
            if (split) archive = await new SplitArchiveService(engine).CompressAsync(request, 65536);
            else await engine.CompressAsync(request);
            var entries = await engine.ListAsync(archive);
            Assert.IsTrue(entries.Single().IsEncrypted);
            var error = await Assert.ThrowsExactlyAsync<SharpCompress.Common.InvalidFormatException>(() => engine.ExtractAsync(new ExtractionRequest(archive, Path.Combine(root, "wrong"), "wrong")));
            Assert.AreEqual("bad password", error.Message);
            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(archive, output, request.Password));
            CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(Path.Combine(output, Path.GetFileName(input))));
            await engine.TestAsync(archive, request.Password);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    [DataRow(true, 0)]
    public async Task QuickExtractionAvoidsOnlyASingleRootFolder(bool enabled, int folderCount)
    {
        var root = Directory.CreateTempSubdirectory("z-root-folder-").FullName;
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            var sources = new List<string>();
            for (var i = 0; i < folderCount; i++)
            {
                var folder = Directory.CreateDirectory(Path.Combine(source, "folder" + i)).FullName;
                await File.WriteAllTextAsync(Path.Combine(folder, "file.txt"), "content");
                sources.Add(folder);
            }
            if (folderCount == 0)
            {
                var file = Path.Combine(source, "file.txt");
                await File.WriteAllTextAsync(file, "content");
                sources.Add(file);
            }
            var engine = new SharpCompressArchiveEngine();
            var archive = Path.Combine(root, "archive.zip");
            await engine.CompressAsync(new CompressionRequest(sources, archive, ArchiveFormat.Zip));
            var destination = Path.Combine(root, "destination");
            var quick = new QuickArchiveService(engine);
            var first = await quick.ExtractAsync(archive, destinationDirectory: destination, avoidDuplicateFolder: enabled);
            var flattened = enabled && folderCount == 1;
            Assert.AreEqual(flattened ? "folder0" : "archive", Path.GetFileName(first));
            Assert.IsTrue(File.Exists(Path.Combine(first, folderCount > 0 && !flattened ? "folder0/file.txt" : "file.txt")));
            var second = await quick.ExtractAsync(archive, destinationDirectory: destination, avoidDuplicateFolder: enabled);
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(Directory.GetDirectories(destination).Any(path => Path.GetFileName(path).StartsWith(".z-compression-")));
        }
        finally { Directory.Delete(root, true); }
    }
}
