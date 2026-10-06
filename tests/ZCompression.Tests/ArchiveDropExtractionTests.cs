using System.IO.Compression;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class ArchiveDropExtractionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DirectDropWritesOnlySelectedContentAtDestination(bool wholeArchive)
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-direct-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "Codex.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (var path in new[] { "outer/folder/한글.txt", "outer/other.txt" })
                { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write(path); }
                zip.CreateEntry("outer/folder/empty/");
            }
            var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
            var engine = new SharpCompressArchiveEngine();
            var entries = await engine.ListAsync(archive);
            string[] selected = ["outer/folder"];
            var manifest = wholeArchive ? ArchiveExportManifest.CreateWholeArchive(entries, archive) : ArchiveExportManifest.Create(entries, selected, "outer");
            Assert.AreEqual(0, ArchiveDropExtraction.CountExistingFiles(destination, archive, manifest));
            var request = ArchiveDropExtraction.CreateRequest(archive, destination, wholeArchive, selected, "outer");
            var reports = new List<ArchiveProgress>();
            await engine.ExtractAsync(request, new CaptureProgress(reports.Add));
            var actualRoot = wholeArchive ? Path.Combine(destination, "Codex", "outer") : destination;
            Assert.AreEqual("outer/folder/한글.txt", await File.ReadAllTextAsync(Path.Combine(actualRoot, "folder", "한글.txt")));
            Assert.IsTrue(Directory.Exists(Path.Combine(actualRoot, "folder", "empty")));
            Assert.AreEqual(wholeArchive, File.Exists(Path.Combine(actualRoot, "other.txt")));
            Assert.AreEqual(100d, reports[^1].Percent);
            Assert.AreEqual(reports[^1].TotalFiles, reports[^1].CompletedFiles);
            Assert.AreEqual(wholeArchive ? 2 : 1, ArchiveDropExtraction.CountExistingFiles(destination, archive, manifest));
            Assert.HasCount(wholeArchive ? 3 : 2, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void DropPreflightRejectsSourceOverwriteAndTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "source.zip");
            Assert.ThrowsExactly<InvalidDataException>(() => ArchiveDropExtraction.CountExistingFiles(root, archive, [new("source.zip", "source.zip", false, 1, null)]));
            Assert.ThrowsExactly<InvalidDataException>(() => ArchiveDropExtraction.CountExistingFiles(root, archive, [new("../escaped.txt", "../escaped.txt", false, 1, null)]));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class CaptureProgress(Action<ArchiveProgress> report) : IProgress<ArchiveProgress>
    { public void Report(ArchiveProgress value) => report(value); }
}
