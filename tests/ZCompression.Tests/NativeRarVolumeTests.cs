using ZCompression.Core.Archives;

namespace ZCompression.Tests;

[TestClass]
public sealed class NativeRarVolumeTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(0)]
    public async Task RarPartNamesWithNumericBaseNameExtractFromFirstPart(int digits)
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-rar-parts-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.multi.part01.rar");
            var engine = new SharpCompressArchiveEngine();
            var expected = await engine.ListAsync(fixture);
            for (var number = 1; number <= 6; number++)
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"Rar5.multi.part{number:D2}.rar"),
                    Path.Combine(root, "Game-RJ01731048.part" + number.ToString("D" + (digits == 0 ? number % 2 + 1 : digits)) + ".rar"));
            var first = RarVolumeSet.FindParts(Path.Combine(root, "Game-RJ01731048.part1.rar"))[0].FullName;
            var actual = await engine.ListAsync(first);
            CollectionAssert.AreEqual(expected.Select(entry => entry.Path).ToArray(), actual.Select(entry => entry.Path).ToArray());
            await engine.TestAsync(first);
            await engine.ExtractAsync(new ExtractionRequest(first, Path.Combine(root, "output")));
            var second = RarVolumeSet.FindParts(first)[1].FullName;
            await engine.TestAsync(second);
            Assert.AreEqual(first, SplitArchiveService.GetFirstVolume(second));
            Assert.AreEqual("Game-RJ01731048", QuickArchiveService.GetExtractionFolderName(first));
            Assert.IsTrue(SplitArchiveService.IsSameArchive(first, second));
            File.Delete(second);
            try { await engine.ListAsync(first); Assert.Fail("Missing middle volume was accepted."); }
            catch (InvalidDataException exception) { StringAssert.Contains(exception.Message, "2"); }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task MissingLastRarPartReportsIncompleteDownloadBeforeWritingFiles()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "z-rar-incomplete-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var first = Path.Combine(root, "archive.part1.rar");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rar5.multi.part01.rar"), first);
            var engine = new SharpCompressArchiveEngine();
            try { await engine.ExtractAsync(new ExtractionRequest(first, Path.Combine(root, "output"))); Assert.Fail("Incomplete archive was accepted."); }
            catch (InvalidDataException exception) { StringAssert.Contains(exception.Message, "Missing or incomplete RAR volumes"); }
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(root, "output"), "*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }
}
