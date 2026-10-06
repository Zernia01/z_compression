namespace ZCompression.Core.Archives;

public sealed record ArchiveExportEntry(string Name, string ArchivePath, bool IsDirectory, long Size, DateTime? Modified, bool IsEncrypted = false);

public static class ArchiveExportManifest
{
    public static IReadOnlyList<ArchiveExportEntry> Create(IReadOnlyList<ArchiveEntryInfo> entries, IReadOnlyList<string>? selectedPaths, string currentFolder)
    {
        var selection = new ArchiveExtractionSelection(new ExtractionRequest("manifest", Path.GetTempPath(), SelectedPaths: selectedPaths, RelativeRoot: currentFolder));
        var result = new Dictionary<string, ArchiveExportEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var relative = selection.GetRelativePath(entry.Path);
            if (relative is null) continue;
            if (entry.IsLink) throw new InvalidDataException("Archive links cannot be exported.");
            if (relative.Length >= 260 || relative.Split('/').Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidDataException("The selected path cannot be represented as a Windows virtual file.");
            if (entry.OriginalSize < 0) throw new InvalidDataException("Archive entry has an invalid size.");
            var parts = relative.Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                var parent = string.Join('/', parts.Take(i));
                result.TryAdd(parent, new(parent.Replace('/', '\\'), "", true, 0, null));
            }
            if (result.TryGetValue(relative, out var existing) && (!existing.IsDirectory || !entry.IsDirectory))
                throw new InvalidDataException("Selected archive entries contain conflicting Windows paths.");
            result[relative] = new(relative.Replace('/', '\\'), entry.Path, entry.IsDirectory, entry.OriginalSize, entry.Modified, entry.IsEncrypted);
        }
        if (result.Count == 0) throw new InvalidDataException("No selected archive entries were found.");
        if (result.Count > 1_000_000 || result.Values.Where(entry => !entry.IsDirectory).Sum(entry => entry.Size) > 100L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Archive selection exceeds extraction limits.");
        // Create parents first, then preserve archive order so solid readers can continue forward.
        return result.Values.Where(entry => entry.IsDirectory).OrderBy(entry => entry.Name.Count(character => character == '\\'))
            .Concat(result.Values.Where(entry => !entry.IsDirectory)).ToArray();
    }

    public static IReadOnlyList<ArchiveExportEntry> CreateWholeArchive(IReadOnlyList<ArchiveEntryInfo> entries, string archivePath)
    {
        var name = QuickArchiveService.GetExtractionFolderName(archivePath);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new InvalidDataException("The archive folder name is invalid.");
        var children = entries.Count == 0 ? [] : Create(entries, null, "");
        var result = new List<ArchiveExportEntry> { new(name, "", true, 0, null) };
        foreach (var entry in children)
        {
            var relative = name + "\\" + entry.Name;
            if (relative.Length >= 260) throw new InvalidDataException("The archive export path is too long.");
            result.Add(entry with { Name = relative });
        }
        return result;
    }
}
