namespace ZCompression.Core.Archives;

public sealed class QuickArchiveService(IArchiveEngine engine)
{
    public async Task<string> CompressAsync(IReadOnlyList<string> sources, ArchiveFormat format = ArchiveFormat.Zip,
        CompressionPreset level = CompressionPreset.High, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (sources.Count == 0) throw new ArgumentException("At least one source is required.", nameof(sources));
        var paths = sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var first = paths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(first) ?? throw new ArgumentException("Select a file or folder, rather than a drive root.", nameof(sources));
        var name = Directory.Exists(first) ? Path.GetFileName(first) : Path.GetFileNameWithoutExtension(first);
        var extension = format switch
        {
            ArchiveFormat.Zip => ".zip", ArchiveFormat.SevenZip => ".7z", ArchiveFormat.Rar => ".rar",
            ArchiveFormat.Tar => ".tar", ArchiveFormat.TarGZip => ".tar.gz",
            _ => throw new NotSupportedException("This format is not available for quick compression."),
        };
        var temporary = Path.Combine(parent, $".z-compression-{Guid.NewGuid():N}{extension}");
        try
        {
            await engine.CompressAsync(new CompressionRequest(paths, temporary, format, level), progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Move with overwrite disabled: concurrent invocations also preserve existing files.
            return Publish(parent, string.IsNullOrWhiteSpace(name) ? "Archive" : name, extension, temporary, isDirectory: false, cancellationToken);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<string> ExtractAsync(string archivePath, string? password = null,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var archive = Path.GetFullPath(archivePath);
        var parent = Path.GetDirectoryName(archive)!;
        var name = GetExtractionFolderName(archive);
        var staging = Path.Combine(parent, $".z-compression-{Guid.NewGuid():N}.extract");
        try
        {
            await engine.ExtractAsync(new ExtractionRequest(archive, staging, password), progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Publish(parent, name, "", staging, isDirectory: true, cancellationToken);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    public static string GetExtractionFolderName(string archivePath)
    {
        var name = Path.GetFileName(archivePath);
        foreach (var extension in new[] { ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst" })
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return name.Length == extension.Length ? "Archive" : name[..^extension.Length];
        }
        var stem = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(stem) ? "Archive" : stem;
    }

    private static string Publish(string parent, string name, string extension, string temporary, bool isDirectory, CancellationToken token)
    {
        for (var number = 1; ; number++)
        {
            token.ThrowIfCancellationRequested();
            var destination = Path.Combine(parent, name + (number == 1 ? "" : $" ({number})") + extension);
            if (File.Exists(destination) || Directory.Exists(destination)) continue;
            try
            {
                if (isDirectory) Directory.Move(temporary, destination);
                else File.Move(temporary, destination, overwrite: false);
                return destination;
            }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
        }
    }
}
