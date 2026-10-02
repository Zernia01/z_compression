using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using ZCompression.Core.Security;

namespace ZCompression.Core.Archives;

public sealed class SharpCompressArchiveEngine : IArchiveEngine
{
    private const int ArchiveReadBufferSize = 1024 * 1024;
    private readonly object _listingCacheLock = new();
    private ListingCache? _listingCache;
    private static readonly IReadOnlyDictionary<ArchiveFormat, ArchiveCapabilities> Supported =
        new Dictionary<ArchiveFormat, ArchiveCapabilities>
        {
            [ArchiveFormat.Zip] = new(true, true, false, true, "Encrypted ZIP reading is supported; creation is not exposed by this engine version."),
            [ArchiveFormat.SevenZip] = new(true, true, false, true, "7Z AES creation is not exposed by the selected engine."),
            [ArchiveFormat.Tar] = new(true, true, false, true),
            [ArchiveFormat.TarGZip] = new(true, true, false, true),
            [ArchiveFormat.GZip] = new(true, true, false, false, "Single-file stream format."),
            [ArchiveFormat.BZip2] = new(true, false, false, false, "Standalone BZ2 creation is not exposed by the archive writer."),
            [ArchiveFormat.Xz] = new(true, false, false, false, "Read-only in SharpCompress."),
            [ArchiveFormat.Zstandard] = new(true, false, false, false, "TAR.ZST is read-only in SharpCompress."),
            [ArchiveFormat.Rar] = new(true, false, false, false, "RAR/RAR5 extraction only; creation is proprietary."),
        };

    public IReadOnlyDictionary<ArchiveFormat, ArchiveCapabilities> Capabilities => Supported;

    public Task CompressAsync(CompressionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Supported.TryGetValue(request.Format, out var capabilities) || !capabilities.CanWrite)
        {
            throw new NotSupportedException($"Creating {request.Format} archives is not supported.");
        }

        if (request.Sources.Count == 0) throw new ArgumentException("At least one source is required.", nameof(request));
        InvalidateListingCache(request.Destination);
        return Task.Run(() => CompressCore(request, progress, cancellationToken), cancellationToken);
    }

    public Task ExtractAsync(ExtractionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => ExtractCore(request, progress, cancellationToken), cancellationToken);

    public Task UpdateAsync(ArchiveUpdateRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Supported.TryGetValue(request.Format, out var capabilities) || !capabilities.CanModify)
            throw new NotSupportedException($"Updating {request.Format} archives is not supported.");
        if (request.Sources.Count == 0) throw new ArgumentException("At least one source is required.", nameof(request));
        return Task.Run(() => UpdateCore(request, progress, cancellationToken), cancellationToken);
    }

    public Task ExtractEntryAsync(string archivePath, string entryPath, string destinationPath, string? password = null, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(entryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            cancellationToken.ThrowIfCancellationRequested();
            using var archive = ArchiveFactory.OpenArchive(archivePath, CreateReaderOptions(password, null, archivePath));
            var normalized = entryPath.Replace('\\', '/').TrimStart('/');
            var entry = archive.Entries.FirstOrDefault(candidate =>
                string.Equals((candidate.Key ?? string.Empty).Replace('\\', '/').TrimStart('/'), normalized, StringComparison.Ordinal));
            if (entry is null || entry.IsDirectory) throw new FileNotFoundException("The selected archive entry was not found.", entryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
            using var input = entry.OpenEntryStream();
            using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
            input.CopyTo(output, 1024 * 1024);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);

    public Task<IReadOnlyList<ArchiveEntryInfo>> ListAsync(string archivePath, string? password = null, System.Text.Encoding? legacyEncoding = null, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<ArchiveEntryInfo>>(() =>
        {
            var file = new FileInfo(Path.GetFullPath(archivePath));
            if (password is null && legacyEncoding is null)
            {
                lock (_listingCacheLock)
                {
                    if (_listingCache is { } cached && cached.Path.Equals(file.FullName, StringComparison.OrdinalIgnoreCase) && cached.Length == file.Length && cached.LastWriteTimeUtc == file.LastWriteTimeUtc)
                        return cached.Entries;
                }
            }

            using var archive = ArchiveFactory.OpenArchive(file, CreateReaderOptions(password, legacyEncoding, file.FullName));
            var entries = archive.Entries.Select(entry =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ArchiveEntryInfo(entry.Key ?? string.Empty, entry.Key ?? string.Empty, entry.Size, entry.CompressedSize, entry.LastModifiedTime, entry.Crc.ToString("X8"), entry.IsDirectory);
                })
                .ToArray();
            if (password is null && legacyEncoding is null)
            {
                lock (_listingCacheLock) _listingCache = new ListingCache(file.FullName, file.Length, file.LastWriteTimeUtc, entries);
            }
            return entries;
        }, cancellationToken);

    public Task TestAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, CreateReaderOptions(password, null, archivePath));
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = entry.OpenEntryStream();
                stream.CopyTo(Stream.Null);
            }
        }, cancellationToken);

    private static void CompressCore(CompressionRequest request, IProgress<ArchiveProgress>? progress, CancellationToken token)
    {
        if (!string.IsNullOrEmpty(request.Password)) throw new NotSupportedException("Encrypted archive creation is not supported by the selected engine.");
        var files = ExpandSources(request.Sources).ToArray();
        var total = files.Where(file => !file.IsDirectory).Sum(file => new FileInfo(file.FullPath).Length);
        var processed = 0L;
        var completed = 0;
        var watch = Stopwatch.StartNew();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Destination))!);

        using var output = new FileStream(request.Destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
        var writerOptions = new WriterOptions(ToCompressionType(request.Format))
        {
            LeaveStreamOpen = false,
            BufferSize = 1024 * 1024,
        };
        if (request.Format != ArchiveFormat.SevenZip) writerOptions.CompressionLevel = ToLevel(request.Level);
        using var writer = WriterFactory.OpenWriter(output, ToArchiveType(request.Format), writerOptions);

        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            Report(progress, file.EntryName, completed, files.Length, processed, total, watch.Elapsed);
            if (file.IsDirectory)
            {
                writer.WriteDirectory(file.EntryName, Directory.GetLastWriteTime(file.FullPath));
                completed++;
                Report(progress, file.EntryName, completed, files.Length, processed, total, watch.Elapsed);
                continue;
            }
            using var input = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
            writer.Write(file.EntryName, input, File.GetLastWriteTime(file.FullPath));
            processed += input.Length;
            completed++;
            Report(progress, file.EntryName, completed, files.Length, processed, total, watch.Elapsed);
        }
    }

    private static void ExtractCore(ExtractionRequest request, IProgress<ArchiveProgress>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(request.DestinationDirectory);
        using var archive = ArchiveFactory.OpenArchive(request.ArchivePath, CreateReaderOptions(request.Password, request.LegacyEncoding, request.ArchivePath));
        var entries = archive.Entries.ToArray();
        if (entries.Length > request.MaximumFileCount) throw new InvalidDataException("Archive contains too many entries.");
        var total = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        if (total > request.MaximumExpandedBytes) throw new InvalidDataException("Archive exceeds the configured expanded-size limit.");

        var processed = 0L;
        var completed = 0;
        var watch = Stopwatch.StartNew();
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var key = entry.Key ?? throw new InvalidDataException("Archive entry has no name.");
            Report(progress, key, completed, entries.Length, processed, total, watch.Elapsed);
            var destination = SafeExtractionPath.Resolve(request.DestinationDirectory, key);
            SafeExtractionPath.EnsureNoReparsePointAncestors(request.DestinationDirectory, destination);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destination);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.OpenEntryStream();
                using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
                input.CopyTo(output, 1024 * 1024);
                processed += entry.Size;
                if (entry.LastModifiedTime is { } modified) File.SetLastWriteTime(destination, modified);
            }
            completed++;
            Report(progress, key, completed, entries.Length, processed, total, watch.Elapsed);
        }
    }

    private void UpdateCore(ArchiveUpdateRequest request, IProgress<ArchiveProgress>? progress, CancellationToken token)
    {
        var archivePath = Path.GetFullPath(request.ArchivePath);
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Archive was not found.", archivePath);

        var workDirectory = Path.Combine(Path.GetTempPath(), "z_compression", "update", Guid.NewGuid().ToString("N"));
        var temporaryArchive = Path.Combine(Path.GetDirectoryName(archivePath)!, $".{Path.GetFileName(archivePath)}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(workDirectory);
        try
        {
            ExtractCore(new ExtractionRequest(archivePath, workDirectory), progress, token);
            var destination = string.IsNullOrWhiteSpace(request.DestinationFolder)
                ? workDirectory
                : SafeExtractionPath.Resolve(workDirectory, request.DestinationFolder.Replace('\\', '/').Trim('/'));
            Directory.CreateDirectory(destination);
            foreach (var source in request.Sources) CopySourceIntoDirectory(source, destination, token);

            var sources = Directory.EnumerateFileSystemEntries(workDirectory).ToArray();
            CompressCore(new CompressionRequest(sources, temporaryArchive, request.Format, request.Level), progress, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporaryArchive, archivePath, true);
            InvalidateListingCache(archivePath);
        }
        finally
        {
            try { if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive); } catch (IOException) { }
            try { if (Directory.Exists(workDirectory)) Directory.Delete(workDirectory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void CopySourceIntoDirectory(string source, string destinationDirectory, CancellationToken token)
    {
        var fullSource = Path.GetFullPath(source);
        token.ThrowIfCancellationRequested();
        if (File.Exists(fullSource))
        {
            RejectReparsePoint(fullSource);
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(fullSource));
            if (Directory.Exists(destination)) throw new IOException($"A directory named '{Path.GetFileName(fullSource)}' already exists in the archive.");
            File.Copy(fullSource, destination, true);
            return;
        }
        if (!Directory.Exists(fullSource)) throw new FileNotFoundException("Source path was not found.", source);

        RejectReparsePoint(fullSource);
        var rootDestination = Path.Combine(destinationDirectory, Path.GetFileName(fullSource.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        if (File.Exists(rootDestination)) throw new IOException($"A file named '{Path.GetFileName(fullSource)}' already exists in the archive.");
        CopyDirectory(fullSource, rootDestination, token);
    }

    private static void CopyDirectory(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RejectReparsePoint(source);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            token.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), token);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Symbolic links and junctions cannot be added to an archive.");
    }

    private static ReaderOptions CreateReaderOptions(string? password, System.Text.Encoding? encoding, string archivePath) => new()
    {
        Password = password,
        ArchiveEncoding = new ArchiveEncoding { Default = encoding ?? System.Text.Encoding.UTF8 },
        BufferSize = ArchiveReadBufferSize,
        ExtensionHint = GetExtensionHint(archivePath),
    };

    private static string GetExtensionHint(string archivePath)
    {
        var name = Path.GetFileName(archivePath);
        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)) return "tar.gz";
        return Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
    }

    private void InvalidateListingCache(string archivePath)
    {
        var fullPath = Path.GetFullPath(archivePath);
        lock (_listingCacheLock)
        {
            if (_listingCache?.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase) == true) _listingCache = null;
        }
    }

    private sealed record ListingCache(string Path, long Length, DateTime LastWriteTimeUtc, IReadOnlyList<ArchiveEntryInfo> Entries);

    private static IEnumerable<(string FullPath, string EntryName, bool IsDirectory)> ExpandSources(IEnumerable<string> sources)
    {
        foreach (var source in sources)
        {
            var full = Path.GetFullPath(source);
            if (File.Exists(full))
            {
                yield return (full, Path.GetFileName(full), false);
                continue;
            }
            if (!Directory.Exists(full)) throw new FileNotFoundException("Source path was not found.", source);
            var parent = Path.GetDirectoryName(full)!;
            foreach (var directory in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories).Prepend(full))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    yield return (directory, Path.GetRelativePath(parent, directory).Replace('\\', '/') + "/", true);
            }
            foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                yield return (file, Path.GetRelativePath(parent, file).Replace('\\', '/'), false);
            }
        }
    }

    private static ArchiveType ToArchiveType(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => ArchiveType.Zip,
        ArchiveFormat.SevenZip => ArchiveType.SevenZip,
        ArchiveFormat.Tar or ArchiveFormat.TarGZip => ArchiveType.Tar,
        ArchiveFormat.GZip => ArchiveType.GZip,
        _ => throw new NotSupportedException($"Creating {format} is not supported."),
    };

    private static CompressionType ToCompressionType(ArchiveFormat format) => format switch
    {
        ArchiveFormat.SevenZip => CompressionType.LZMA2,
        ArchiveFormat.TarGZip or ArchiveFormat.GZip => CompressionType.GZip,
        ArchiveFormat.BZip2 => CompressionType.BZip2,
        ArchiveFormat.Tar => CompressionType.None,
        _ => CompressionType.Deflate,
    };

    private static int ToLevel(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Store => 0,
        CompressionPreset.Fastest => 1,
        CompressionPreset.Fast => 3,
        CompressionPreset.Normal => 6,
        CompressionPreset.High => 8,
        CompressionPreset.Ultra => 9,
        _ => 5,
    };

    private static void Report(IProgress<ArchiveProgress>? progress, string current, int completed, int count, long processed, long total, TimeSpan elapsed)
    {
        var percent = total == 0 ? (count == 0 ? 100 : completed * 100d / count) : processed * 100d / total;
        var speed = elapsed.TotalSeconds <= 0 ? 0 : processed / elapsed.TotalSeconds;
        progress?.Report(new ArchiveProgress(percent, current, completed, count, processed, total, speed, elapsed));
    }
}
