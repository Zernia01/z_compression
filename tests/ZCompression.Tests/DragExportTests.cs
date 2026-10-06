using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
    public void FormatChecksDoNotExtract_FileRequestsPrepareOnlyOnce()
    {
        var prepared = 0;
        var data = new DeferredArchiveDataObject(new FakeDataObject(), 15, () => prepared++);
        var format = new FORMATETC { cfFormat = 15, tymed = TYMED.TYMED_HGLOBAL };
        data.QueryGetData(ref format);
        Assert.AreEqual(0, prepared);
        data.GetData(ref format, out _);
        data.GetData(ref format, out _);
        Assert.AreEqual(1, prepared);
    }

    [TestMethod]
    public void FailedExportDoesNotReturnPartialFilesOrRetryExtraction()
    {
        var prepared = 0;
        var data = new DeferredArchiveDataObject(new FakeDataObject(), 15, () => { prepared++; throw new OperationCanceledException(); });
        var format = new FORMATETC { cfFormat = 15, tymed = TYMED.TYMED_HGLOBAL };
        Assert.ThrowsExactly<COMException>(() => data.GetData(ref format, out _));
        Assert.ThrowsExactly<COMException>(() => data.GetData(ref format, out _));
        Assert.IsInstanceOfType<OperationCanceledException>(data.Error);
        Assert.AreEqual(1, prepared);
    }

    private sealed class FakeDataObject : IDataObject
    {
        public void GetData(ref FORMATETC format, out STGMEDIUM medium) => medium = default;
        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) { }
        public int QueryGetData(ref FORMATETC format) => 0;
        public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output) { output = input; return 0; }
        public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) { }
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => throw new NotSupportedException();
        public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) { connection = 0; return 0; }
        public void DUnadvise(int connection) { }
        public int EnumDAdvise(out IEnumSTATDATA enumerator) { enumerator = null!; return 1; }
    }
}
