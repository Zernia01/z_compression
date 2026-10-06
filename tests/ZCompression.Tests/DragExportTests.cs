using System.IO.Compression;
using ZCompression.App;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class DragExportTests
{
    [TestMethod]
    public void TransferProgressCountsUniqueBytesAndCompletedFiles_WhenStreamsAreReread()
    {
        var reports = new List<ArchiveProgress>();
        var progress = new ArchiveTransferProgress([new("folder", "", true, 0, null), new("a", "a", false, 8, null), new("empty", "empty", false, 0, null)], new CaptureProgress(reports.Add));
        progress.Report("a", 4, false);
        Assert.AreEqual(50d, reports[0].Percent);
        progress.Report("a", 8, true);
        progress.Report("a", 2, false); // A clone/rewind must not count the bytes twice.
        progress.Report("a", 8, true);
        progress.Report("empty", 0, true);
        var last = reports[^1];
        Assert.AreEqual(8L, last.ProcessedBytes);
        Assert.AreEqual(8L, last.TotalBytes);
        Assert.AreEqual(2, last.CompletedFiles);
        Assert.AreEqual(2, last.TotalFiles);
        Assert.AreEqual(100d, last.Percent);
    }

    private sealed class CaptureProgress(Action<ArchiveProgress> report) : IProgress<ArchiveProgress>
    { public void Report(ArchiveProgress value) => report(value); }

    [TestMethod]
    public async Task ReusableSessionStopsReadingWhenCancelled_AndReleasesArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-cancel-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "cancel.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (var output = zip.CreateEntry("payload.bin").Open()) output.Write(new byte[256 * 1024]);
            using var cancellation = new CancellationTokenSource();
            using (var session = new SharpCompressArchiveEngine().CreateEntryReadSession(archive, cancellationToken: cancellation.Token))
            using (var input = session.Open("payload.bin"))
            {
                Assert.AreEqual(1, input.Read(new byte[1], 0, 1));
                await cancellation.CancelAsync();
                Assert.ThrowsExactly<OperationCanceledException>(() => input.Read(new byte[1], 0, 1));
            }
            using var released = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow(ArchiveFormat.Zip, ".zip")]
    [DataRow(ArchiveFormat.SevenZip, ".7z")]
    [DataRow(ArchiveFormat.TarGZip, ".tar.gz")]
    public async Task ReusableSessionReadsAllFilesAndRepeatedRequests_WithoutExpandedTemporaryFiles(ArchiveFormat format, string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-session-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            foreach (var name in new[] { "z-last.bin", "a-first.bin", "empty.bin" })
                await File.WriteAllBytesAsync(Path.Combine(source, name), name == "empty.bin" ? [] : Enumerable.Range(0, 300000).Select(i => (byte)i).ToArray());
            var archive = Path.Combine(root, "source" + extension);
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, format));
            var entries = await engine.ListAsync(archive);
            var manifest = ArchiveExportManifest.Create(entries, null, "");
            CollectionAssert.AreEqual(entries.Where(entry => !entry.IsDirectory).Select(entry => entry.Path).ToArray(), manifest.Where(entry => !entry.IsDirectory).Select(entry => entry.ArchivePath).ToArray());
            using (var session = engine.CreateEntryReadSession(archive))
            {
                using (var probe = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } // Still lazy.
                foreach (var entry in manifest.Where(entry => !entry.IsDirectory).Concat(manifest.Where(entry => !entry.IsDirectory).Reverse()))
                {
                    using var input = session.Open(entry.ArchivePath);
                    using var output = new MemoryStream();
                    await input.CopyToAsync(output);
                    CollectionAssert.AreEqual(await File.ReadAllBytesAsync(Path.Combine(root, entry.ArchivePath.Replace('/', Path.DirectorySeparatorChar))), output.ToArray());
                }
            }
            Assert.HasCount(4, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            using var released = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SelectedFolderAndFiles_KeepNamesAndExcludeSiblingsAndParentPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-drag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "selection.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "outer/폴더/한글.txt", "outer/폴더/nested/zero.bin", "outer/pick.txt", "outer/unselected.txt", "outer/폴더-other/no.txt" })
                {
                    using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                    writer.Write(name);
                }
                zip.CreateEntry("outer/폴더/empty/");
            }
            var destination = Path.Combine(root, "out");
            var engine = new SharpCompressArchiveEngine();
            await engine.ExtractAsync(new ExtractionRequest(archive, destination, SelectedPaths: ["outer/폴더", "outer/pick.txt"], RelativeRoot: "outer"));
            Assert.AreEqual("outer/폴더/한글.txt", await File.ReadAllTextAsync(Path.Combine(destination, "폴더", "한글.txt")));
            Assert.IsTrue(Directory.Exists(Path.Combine(destination, "폴더", "empty")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "pick.txt")));
            Assert.HasCount(3, Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "outer")));
            Assert.IsFalse(Directory.Exists(Path.Combine(destination, "폴더-other")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SelectedExtraction_RejectsTraversalBeforeRemovingCurrentFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-drag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "unsafe.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) zip.CreateEntry("outer/folder/../../escaped.txt");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new SharpCompressArchiveEngine().ExtractAsync(
                new ExtractionRequest(archive, Path.Combine(root, "out"), SelectedPaths: ["outer/folder"], RelativeRoot: "outer")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "escaped.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SevenZipSelection_ExtractsNestedFolderWithoutItsParent()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-drag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            Directory.CreateDirectory(Path.Combine(source, "folder", "empty"));
            await File.WriteAllTextAsync(Path.Combine(source, "folder", "한글.txt"), "seven zip");
            await File.WriteAllTextAsync(Path.Combine(source, "other.txt"), "excluded");
            var engine = new SharpCompressArchiveEngine();
            var archive = Path.Combine(root, "selection.7z");
            await engine.CompressAsync(new CompressionRequest([source], archive, ArchiveFormat.SevenZip));
            var destination = Path.Combine(root, "out");
            await engine.ExtractAsync(new ExtractionRequest(archive, destination, SelectedPaths: ["source/folder"], RelativeRoot: "source"));
            Assert.AreEqual("seven zip", await File.ReadAllTextAsync(Path.Combine(destination, "folder", "한글.txt")));
            Assert.IsTrue(Directory.Exists(Path.Combine(destination, "folder", "empty")));
            Assert.HasCount(1, Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ManifestIncludesImplicitAndEmptyDirectories_WithoutOpeningFiles()
    {
        ArchiveEntryInfo Entry(string path, bool folder = false) => new(path, path, folder ? 0 : 5, 1, null, null, folder);
        var manifest = ArchiveExportManifest.Create([Entry("outer/folder/nested/a.txt"), Entry("outer/folder/empty/", true), Entry("outer/other.txt")], ["outer/folder"], "outer");
        Assert.HasCount(4, manifest);
        Assert.IsTrue(manifest.Any(entry => entry.Name == @"folder\empty" && entry.IsDirectory));
        Assert.IsTrue(manifest.Any(entry => entry.Name == @"folder\nested\a.txt" && !entry.IsDirectory));
        Assert.IsFalse(manifest.Any(entry => entry.Name.Contains("outer", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void StreamMetadataAndSeekDoNotDecompress_ReadStartsIncrementally()
    {
        var opens = 0;
        var bytes = Enumerable.Range(0, 200000).Select(value => (byte)value).ToArray();
        using var stream = new ArchiveContentStream("large.bin", bytes.Length, () => { opens++; return new MemoryStream(bytes); }, error => throw new AssertFailedException(error.Message), _ => { });
        stream.Stat(out var stat, 0);
        stream.Seek(17, 0, IntPtr.Zero);
        stream.Clone(out var clone);
        Assert.AreEqual(0, opens);
        Assert.AreEqual((long)bytes.Length, stat.cbSize);
        var buffer = new byte[13];
        stream.Read(buffer, buffer.Length, IntPtr.Zero);
        CollectionAssert.AreEqual(bytes.Skip(17).Take(13).ToArray(), buffer);
        Assert.AreEqual(1, opens);
        clone.Read(buffer, buffer.Length, IntPtr.Zero);
        CollectionAssert.AreEqual(bytes.Skip(17).Take(13).ToArray(), buffer);
        ((IDisposable)clone).Dispose();
        stream.Seek(0, 0, IntPtr.Zero);
        stream.Read(buffer, buffer.Length, IntPtr.Zero);
        CollectionAssert.AreEqual(bytes.Take(13).ToArray(), buffer);
    }

    [TestMethod]
    public void ArchiveTitleExportIncludesAllEntriesUnderArchiveNamedFolder()
    {
        var entry = new ArchiveEntryInfo("nested/file.txt", "nested/file.txt", 3, 2, null, null, false);
        var manifest = ArchiveExportManifest.CreateWholeArchive([entry], "Codex.zip");
        Assert.AreEqual("Codex", manifest[0].Name);
        Assert.IsTrue(manifest[0].IsDirectory);
        Assert.IsTrue(manifest.Any(item => item.Name == @"Codex\nested\file.txt" && item.ArchivePath == "nested/file.txt"));
        var empty = ArchiveExportManifest.CreateWholeArchive([], "empty.tar.gz");
        Assert.HasCount(1, empty);
        Assert.AreEqual("empty", empty[0].Name);
    }

    [TestMethod]
    [DataRow(ArchiveFormat.Zip, ".zip")]
    [DataRow(ArchiveFormat.SevenZip, ".7z")]
    public async Task DirectArchiveStreamReturnsContent_WithoutExtractedFiles(ArchiveFormat format, string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-stream-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "한글.txt");
            await File.WriteAllTextAsync(source, "streamed archive content");
            var archive = Path.Combine(root, "source" + extension);
            var engine = new SharpCompressArchiveEngine();
            await engine.CompressAsync(new CompressionRequest([source], archive, format));
            using (var input = engine.OpenEntryReadStream(archive, "한글.txt"))
            using (var reader = new StreamReader(input)) Assert.AreEqual("streamed archive content", await reader.ReadToEndAsync());
            Assert.HasCount(2, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            using var reopened = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(root, true); }
    }
}
