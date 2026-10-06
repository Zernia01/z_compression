using ZCompression.Core.Security;

namespace ZCompression.Core.Archives;

public static class ArchiveDropExtraction
{
    public static ExtractionRequest CreateRequest(string archivePath, string droppedFolder, bool wholeArchive, IReadOnlyList<string>? selectedPaths, string currentFolder, string? password = null)
    {
        var destination = wholeArchive ? SafeExtractionPath.Resolve(droppedFolder, QuickArchiveService.GetExtractionFolderName(archivePath)) : Path.GetFullPath(droppedFolder);
        return new(archivePath, destination, password, SelectedPaths: wholeArchive ? null : selectedPaths, RelativeRoot: wholeArchive ? "" : currentFolder);
    }

    public static int CountExistingFiles(string droppedFolder, string archivePath, IReadOnlyList<ArchiveExportEntry> manifest, CancellationToken token = default)
    {
        var source = Path.GetFullPath(archivePath);
        var count = 0;
        foreach (var entry in manifest)
        {
            token.ThrowIfCancellationRequested();
            var path = SafeExtractionPath.Resolve(droppedFolder, entry.Name);
            SafeExtractionPath.EnsureNoReparsePointAncestors(droppedFolder, path);
            if (entry.IsDirectory) continue;
            if (source.Equals(path, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An archive cannot overwrite itself during extraction.");
            if (File.Exists(path)) count++;
        }
        return count;
    }
}
