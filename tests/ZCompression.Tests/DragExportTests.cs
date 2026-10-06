using System.IO.Compression;
using ZCompression.App;
using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class DragExportTests
{
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
    public async Task FileDeliveryWaitsForAsyncPreparation_AndRepeatDragReusesFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-drag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "한글.txt");
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var export = new PreparedDragExport([path], async () => { calls++; await release.Task; await File.WriteAllTextAsync(path, "ready"); });
            var preparation = export.PrepareAsync();
            Assert.IsFalse(preparation.IsCompleted);
            Assert.IsFalse(export.IsReady);
            Assert.ThrowsExactly<InvalidOperationException>(() => export.GetReadyPaths());
            release.SetResult();
            await preparation.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(export.IsReady);
            await export.PrepareAsync();
            Assert.AreEqual(path, export.GetReadyPaths().Single());
            Assert.AreEqual(1, calls);
            File.Delete(path);
            Assert.IsFalse(export.IsReady);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CancellationDoesNotExposePartialFilePaths()
    {
        var export = new PreparedDragExport(["unused"], () => Task.FromCanceled(new CancellationToken(true)));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => export.PrepareAsync());
        Assert.IsFalse(export.IsReady);
        Assert.ThrowsExactly<InvalidOperationException>(() => export.GetReadyPaths());
    }
}
