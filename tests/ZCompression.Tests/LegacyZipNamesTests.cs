using System.IO.Compression;
using ZCompression.Core.Archives;
using ZCompression.Core.Encoding;

namespace ZCompression.Tests;

[TestClass]
public sealed class LegacyZipNamesTests
{
    [TestMethod]
    [DataRow("cp932", "日本語/説明.txt")]
    [DataRow("cp949", "한글/설명.txt")]
    [DataRow("gbk", "中文/说明.txt")]
    [DataRow("big5", "中文/說明.txt")]
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
            Assert.AreNotEqual(name, (await engine.ListAsync(archive)).Single().Path);
            Assert.AreEqual(name, (await engine.ListAsync(archive, legacyEncoding: encoding)).Single().Path);
            var preview = Path.Combine(root, "preview.txt");
            await engine.ExtractEntryAsync(archive, name, preview, legacyEncoding: encoding);
            Assert.AreEqual("legacy payload", await File.ReadAllTextAsync(preview));

            var output = Path.Combine(root, "output");
            await engine.ExtractAsync(new ExtractionRequest(archive, output, LegacyEncoding: encoding, SelectedPaths: [name]));
            Assert.AreEqual("legacy payload", await File.ReadAllTextAsync(Path.Combine(output, name)));

            var added = Path.Combine(root, "added.txt");
            await File.WriteAllTextAsync(added, "another file");
            await engine.UpdateAsync(new ArchiveUpdateRequest(archive, [added], ArchiveFormat.Zip, LegacyEncoding: encoding));
            CollectionAssert.AreEquivalent(new[] { "added.txt", name }, (await engine.ListAsync(archive)).Select(entry => entry.Path).ToArray());
            Assert.AreEqual(name, (await engine.ListAsync(archive, legacyEncoding: encoding)).Single(entry => entry.Path != "added.txt").Path);
        }
        finally { Directory.Delete(root, true); }
    }
}
