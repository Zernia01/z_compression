using System.IO.Compression;
using ZCompression.Core.Archives;
using ZCompression.Core.Encoding;

namespace ZCompression.Tests;

[TestClass]
public sealed class LegacyZipNamesTests
{
    [TestMethod]
    [DataRow("cp932", "日本語/説明.txt")]
    [DataRow("cp932", "ソフトウェア/設定を登録.reg")]
    [DataRow("cp949", "한글/설명.txt")]
    [DataRow("gbk", "中文/说明.txt")]
    [DataRow("big5", "中文/說明.txt")]
    [DataRow("cp949", "한글폴더/환경설정등록.reg")]
    [DataRow("gbk", "中文文件夹/注册表补丁.reg")]
    [DataRow("big5", "中文檔案夾/註冊表補丁.reg")]
    [DataRow("cp1252", "café/résumé.txt")]
    public async Task LegacyNamesAreConsistentAcrossListingAndExtraction(string codePage, string name)
    {
        var encoding = LegacyEncodingService.Choices.Single(choice => choice.Id == codePage).Encoding!;
        var root = Directory.CreateTempSubdirectory("z-legacy-zip-").FullName;
        try
        {
            var archive = Path.Combine(root, "legacy.zip");
            using (var zip = new ZipArchive(File.Create(archive), ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: encoding))
            using (var content = new StreamWriter(zip.CreateEntry(name).Open()))
                content.Write("legacy payload");

            var engine = new SharpCompressArchiveEngine();
            Assert.AreEqual(name, (await engine.ListAsync(archive)).Single().Path);
            Assert.AreEqual(name, (await engine.ListAsync(archive, legacyEncoding: encoding)).Single().Path);
            var preview = Path.Combine(root, "preview.txt");
            await engine.ExtractEntryAsync(archive, name, preview);
            Assert.AreEqual("legacy payload", await File.ReadAllTextAsync(preview));

            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(archive, output, SelectedPaths: [name]));
            Assert.AreEqual("legacy payload", await File.ReadAllTextAsync(Path.Combine(output, name)));
            var quick = await new QuickArchiveService(engine).ExtractAsync(archive);
            Assert.AreEqual("legacy payload", await File.ReadAllTextAsync(Path.Combine(quick, name)));

            var added = Path.Combine(root, "added.txt");
            await File.WriteAllTextAsync(added, "another file");
            await engine.UpdateAsync(new ArchiveUpdateRequest(archive, [added], ArchiveFormat.Zip, LegacyEncoding: encoding));
            CollectionAssert.AreEquivalent(new[] { "added.txt", name }, (await engine.ListAsync(archive)).Select(entry => entry.Path).ToArray());
            Assert.AreEqual(name, (await engine.ListAsync(archive, legacyEncoding: encoding)).Single(entry => entry.Path != "added.txt").Path);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AutoEncodingHandlesUnmarkedUtf8AndSplitZip(bool split)
    {
        var root = Directory.CreateTempSubdirectory("z-auto-name-").FullName;
        try
        {
            var path = Path.Combine(root, "auto.zip");
            const string name = "日本語/説明.txt";
            var encoding = split ? LegacyEncodingService.Choices.Single(choice => choice.Id == "cp932").Encoding! : System.Text.Encoding.UTF8;
            using (var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create, false, encoding))
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("payload");
            var bytes = await File.ReadAllBytesAsync(path);
            if (split)
            {
                await File.WriteAllBytesAsync(path + ".001", bytes[..(bytes.Length / 2)]);
                await File.WriteAllBytesAsync(path + ".002", bytes[(bytes.Length / 2)..]);
                File.Delete(path);
                path += ".002";
            }
            else
            {
                for (var index = 0; index + 10 < bytes.Length; index++)
                {
                    var signature = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index));
                    if (signature is not (0x04034b50 or 0x02014b50)) continue;
                    var flags = index + (signature == 0x04034b50 ? 6 : 8);
                    bytes[flags + 1] &= 0xf7;
                }
                await File.WriteAllBytesAsync(path, bytes);
            }
            var destination = await new QuickArchiveService(new SharpCompressArchiveEngine()).ExtractAsync(path);
            Assert.AreEqual("payload", await File.ReadAllTextAsync(Path.Combine(destination, name)));
            Assert.AreEqual(root, Path.GetDirectoryName(destination));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task MixedUtf8AndLegacyNamesKeepTheirOwnEncodingFlags()
    {
        var root = Directory.CreateTempSubdirectory("z-mixed-names-").FullName;
        try
        {
            var path = Path.Combine(root, "mixed.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("123456.txt").Open())) writer.Write("legacy");
                using (var writer = new StreamWriter(zip.CreateEntry("한글-日本語-😀.txt").Open())) writer.Write("UTF8");
            }
            var cp932 = LegacyEncodingService.Choices.Single(choice => choice.Id == "cp932").Encoding!;
            var rawName = cp932.GetBytes("日本語.txt");
            var bytes = await File.ReadAllBytesAsync(path);
            var placeholder = System.Text.Encoding.ASCII.GetBytes("123456.txt");
            for (var index = 0; index <= bytes.Length - placeholder.Length; index++)
                if (bytes.AsSpan(index, placeholder.Length).SequenceEqual(placeholder))
                    rawName.CopyTo(bytes.AsSpan(index));
            await File.WriteAllBytesAsync(path, bytes);
            var engine = new SharpCompressArchiveEngine();
            CollectionAssert.AreEquivalent(new[] { "日本語.txt", "한글-日本語-😀.txt" }, (await engine.ListAsync(path)).Select(entry => entry.Path).ToArray());
            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(path, output));
            Assert.AreEqual("legacy", await File.ReadAllTextAsync(Path.Combine(output, "日本語.txt")));
            Assert.AreEqual("UTF8", await File.ReadAllTextAsync(Path.Combine(output, "한글-日本語-😀.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
}
