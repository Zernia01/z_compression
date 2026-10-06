using System.Text;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class RarArchiveTests
{
    [TestMethod]
    [DataRow("Rar.rar", null)]
    [DataRow("Rar.solid.rar", null)]
    [DataRow("Rar5.rar", null)]
    [DataRow("Rar5.solid.rar", null)]
    [DataRow("Rar5.crc_blake2.rar", null)]
    [DataRow("Rar5.multi.part01.rar", null)]
    [DataRow("Rar.encrypted_filesAndHeader.rar", "test")]
    [DataRow("Rar.encrypted_filesOnly.rar", "test")]
    [DataRow("Rar5.encrypted_filesAndHeader.rar", "test")]
    [DataRow("Rar5.encrypted_filesOnly.rar", "test")]
    public async Task RealRar_ListsTestsExtractsAndPreviewsEveryFile(string fixture, string? password)
    {
        var root = TemporaryDirectory();
        try
        {
            var archive = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
            var engine = new SharpCompressArchiveEngine();
            var entries = await engine.ListAsync(archive, password);
            Assert.IsNotEmpty(entries);
            Assert.IsTrue(entries.Any(entry => entry.IsDirectory), "Fixture must include folders to cover the missing-directory-CRC regression.");
            await engine.TestAsync(archive, password);
            var output = Path.Combine(root, "extracted");
            await engine.ExtractAsync(new ExtractionRequest(archive, output, password));
            foreach (var entry in entries)
            {
                var extracted = Path.Combine(output, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                if (entry.IsDirectory)
                {
                    Assert.IsNull(entry.Checksum);
                    Assert.IsTrue(Directory.Exists(extracted));
                    continue;
                }
                Assert.AreEqual(entry.OriginalSize, new FileInfo(extracted).Length);
                var preview = Path.Combine(root, Guid.NewGuid() + ".preview");
                await engine.ExtractEntryAsync(archive, entry.Path, preview, password);
                CollectionAssert.AreEqual(await File.ReadAllBytesAsync(extracted), await File.ReadAllBytesAsync(preview), entry.Path);
                using var direct = engine.OpenEntryReadStream(archive, entry.Path, password);
                using var streamed = new MemoryStream();
                await direct.CopyToAsync(streamed);
                CollectionAssert.AreEqual(await File.ReadAllBytesAsync(extracted), streamed.ToArray(), entry.Path);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OfficialRar_CreationAndUpdateRoundTrip(bool encrypted)
    {
        var tool = RarCommandLine.FindExecutable();
        if (tool is null) Assert.Inconclusive("Install WinRAR or set Z_COMPRESSION_RAR_PATH to run RAR writer integration tests.");
        var root = TemporaryDirectory();
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "한글 source")).FullName;
            Directory.CreateDirectory(Path.Combine(source, "빈 폴더"));
            foreach (var name in new[] { "한글-日本語-中文-😀.txt", "-switch.txt", "@list.txt", "spaces [brackets].txt" })
                await File.WriteAllTextAsync(Path.Combine(source, name), "content: " + name, Encoding.UTF8);
            await File.WriteAllBytesAsync(Path.Combine(source, "empty.bin"), []);
            var password = encrypted ? "test 암호 !@" : null;
            var archive = Path.Combine(root, "한글 archive.rar");
            var engine = new SharpCompressArchiveEngine(tool);
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Rar, CompressionPreset.Ultra, password));
            var magic = await File.ReadAllBytesAsync(archive);
            CollectionAssert.AreEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x01, 0x00 }, magic.Take(8).ToArray());
            await engine.TestAsync(archive, password);
            var entries = await engine.ListAsync(archive, password);
            Assert.IsTrue(entries.Any(entry => entry.Path.EndsWith("빈 폴더", StringComparison.Ordinal)));
            var added = Path.Combine(root, "added.txt");
            await File.WriteAllTextAsync(added, "new content");
            await engine.UpdateAsync(new ArchiveUpdateRequest(archive, [added], ArchiveFormat.Rar, "한글 source/nested", Password: password));
            await engine.TestAsync(archive, password);
            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(archive, output, password));
            foreach (var file in Directory.GetFiles(source))
                CollectionAssert.AreEqual(await File.ReadAllBytesAsync(file), await File.ReadAllBytesAsync(Path.Combine(output, "한글 source", Path.GetFileName(file))));
            Assert.IsTrue(Directory.Exists(Path.Combine(output, "한글 source", "빈 폴더")));
            Assert.AreEqual("new content", await File.ReadAllTextAsync(Path.Combine(output, "한글 source", "nested", "added.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task MissingRarTool_ReportsRequirementAndPreservesDestination()
    {
        var root = TemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source.txt");
            var archive = Path.Combine(root, "existing.rar");
            await File.WriteAllTextAsync(source, "source");
            await File.WriteAllTextAsync(archive, "original");
            var engine = new SharpCompressArchiveEngine(Path.Combine(root, "missing-rar.exe"));
            var error = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Rar)));
            StringAssert.Contains(error.Message, "rar.exe");
            Assert.AreEqual("original", await File.ReadAllTextAsync(archive));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task RarExtraction_RespectsLimitsAndMissingVolumes()
    {
        var root = TemporaryDirectory();
        try
        {
            var engine = new SharpCompressArchiveEngine();
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.solid.rar");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => engine.ExtractAsync(new ExtractionRequest(fixture, Path.Combine(root, "out"), MaximumExpandedBytes: 1)));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(root, "out"), "*", SearchOption.AllDirectories));
            var incomplete = Path.Combine(root, "missing.part01.rar");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.multi.part01.rar"), incomplete);
            await Assert.ThrowsExactlyAsync<SharpCompress.Common.IncompleteArchiveException>(() => engine.ListAsync(incomplete));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string TemporaryDirectory() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-compression-rar-tests", Guid.NewGuid().ToString("N"))).FullName;

    [TestMethod]
    public async Task MultipartRar_UpdateIsRejectedBeforeAnyVolumeIsChanged()
    {
        var root = TemporaryDirectory();
        try
        {
            foreach (var fixture in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures"), "Rar5.multi.part*.rar"))
                File.Copy(fixture, Path.Combine(root, Path.GetFileName(fixture)));
            var volumes = Directory.GetFiles(root).ToDictionary(path => path, File.ReadAllBytes);
            var source = Path.Combine(root, "added.txt");
            await File.WriteAllTextAsync(source, "new");
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => new SharpCompressArchiveEngine().UpdateAsync(new ArchiveUpdateRequest(Path.Combine(root, "Rar5.multi.part01.rar"), [source], ArchiveFormat.Rar)));
            foreach (var volume in volumes) CollectionAssert.AreEqual(volume.Value, await File.ReadAllBytesAsync(volume.Key));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task RarPreview_WrongPasswordCanBeRetriedAtSameDestination()
    {
        var root = TemporaryDirectory();
        try
        {
            var engine = new SharpCompressArchiveEngine();
            var archive = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.encrypted_filesOnly.rar");
            var entry = (await engine.ListAsync(archive)).First(item => !item.IsDirectory);
            var preview = Path.Combine(root, "preview");
            await Assert.ThrowsExactlyAsync<SharpCompress.Common.CryptographicException>(() => engine.ExtractEntryAsync(archive, entry.Path, preview, "wrong"));
            Assert.IsFalse(File.Exists(preview));
            await engine.ExtractEntryAsync(archive, entry.Path, preview, "test");
            Assert.AreEqual(entry.OriginalSize, new FileInfo(preview).Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task RarIntegrity_StillRejectsCorruptFileDataWhenChecksumDisplayIsOmitted()
    {
        var tool = RarCommandLine.FindExecutable();
        if (tool is null) Assert.Inconclusive("RAR writer integration test requires rar.exe.");
        var root = TemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "payload.txt");
            var payload = Encoding.ASCII.GetBytes("CRC regression payload 0123456789");
            await File.WriteAllBytesAsync(source, payload);
            var archive = Path.Combine(root, "corrupt.rar");
            var engine = new SharpCompressArchiveEngine(tool);
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.Rar, CompressionPreset.Store));
            var bytes = await File.ReadAllBytesAsync(archive);
            var position = bytes.AsSpan().IndexOf(payload);
            Assert.IsGreaterThanOrEqualTo(0, position);
            bytes[position] ^= 1;
            await File.WriteAllBytesAsync(archive, bytes);
            Assert.IsNotEmpty(await engine.ListAsync(archive));
            await Assert.ThrowsExactlyAsync<SharpCompress.Common.InvalidFormatException>(() => engine.TestAsync(archive));
        }
        finally { Directory.Delete(root, true); }
    }
}
