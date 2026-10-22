using System.IO.Compression;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class LongPathDragTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DeepPathsCanBeDraggedAndExtracted(bool wholeArchive)
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-long-drag-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var path = string.Join('/', Enumerable.Range(0, 12).Select(index => $"folder-{index:D2}-日本語中文-long-folder")) + "/日本語-中文.txt";
            var archive = Path.Combine(root, "archive.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write("long path content");
            var engine = new SharpCompressArchiveEngine();
            var entries = await engine.ListAsync(archive);
            var selected = new[] { path.Split('/')[0] };
            var manifest = wholeArchive ? ArchiveExportManifest.CreateWholeArchive(entries, archive)
                : ArchiveExportManifest.Create(entries, selected, "");
            Assert.IsTrue(manifest.Any(entry => entry.Name.Length > 260));
            var destination = Path.Combine(root, "output");
            Assert.AreEqual(0, ArchiveDropExtraction.CountExistingFiles(destination, archive, manifest));
            await engine.ExtractAsync(ArchiveDropExtraction.CreateRequest(archive, destination, wholeArchive, wholeArchive ? null : selected, ""));
            var output = Path.Combine(destination, wholeArchive ? "archive" : "", path.Replace('/', Path.DirectorySeparatorChar));
            Assert.AreEqual("long path content", await File.ReadAllTextAsync(output));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void UnusableFileNamesAreReportedAsPathProblems()
    {
        var entry = new ArchiveEntryInfo("bad:name.txt", "folder/bad:name.txt", 1, 1, null, null, false);
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => ArchiveExportManifest.Create([entry], null, ""));
        StringAssert.Contains(exception.Message, "folder/bad:name.txt");
    }
}
