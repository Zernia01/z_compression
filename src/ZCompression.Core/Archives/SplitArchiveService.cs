using System.Globalization;

namespace ZCompression.Core.Archives;

/// <summary>Byte volumes compatible with the .7z.001 / .zip.001 convention.</summary>
public sealed class SplitArchiveService(IArchiveEngine engine)
{
    public static bool IsVolumePath(string path) => Path.GetExtension(path) is { Length: >= 4 } suffix &&
        suffix[1..].All(char.IsAsciiDigit) && int.TryParse(suffix[1..], out var number) && number > 0 &&
        new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst" }.Contains(Path.GetExtension(path[..^suffix.Length]), StringComparer.OrdinalIgnoreCase);

    public static string GetBasePath(string path) => IsVolumePath(path) ? path[..^Path.GetExtension(path).Length] : path;
    public static string GetFirstVolume(string path) => IsVolumePath(path) ? GetBasePath(path) + ".001" : RarVolumeSet.GetFirstPart(path);
    public static bool IsSameArchive(string first, string second)
    {
        if (RarVolumeSet.IsPart(first) != RarVolumeSet.IsPart(second) || IsVolumePath(first) != IsVolumePath(second)) return false;
        static string Key(string path)
        {
            var full = Path.GetFullPath(path);
            if (RarVolumeSet.IsPart(full)) return Path.Combine(Path.GetDirectoryName(full)!, RarVolumeSet.GetArchiveName(full) + ".rar");
            return GetBasePath(full);
        }
        return Key(first).Equals(Key(second), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> CompressAsync(CompressionRequest request, long volumeSize, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (volumeSize < 65536) throw new ArgumentOutOfRangeException(nameof(volumeSize), "Volume size must be at least 64 KiB.");
        var destination = Path.GetFullPath(request.Destination);
        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        if (Directory.EnumerateFileSystemEntries(parent).Any(path => IsVolumePath(path) && GetBasePath(path).Equals(destination, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Split archive volumes already exist. Choose a different archive name. / 같은 이름의 분할 파일이 있습니다. 다른 이름을 지정해 주세요.");
        var work = Path.Combine(Path.GetTempPath(), "z_compression", "split-" + Guid.NewGuid().ToString("N"));
        // The completed archive must stay outside the selected source trees.
        if (request.Sources.Any(source => Directory.Exists(source) && work.StartsWith(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Choose sources outside the system temporary directory when creating split archives.");
        Directory.CreateDirectory(work);
        var staging = Path.Combine(parent, ".z-compression-split-" + Guid.NewGuid().ToString("N"));
        var published = new List<string>();
        var success = false;
        try
        {
            var complete = Path.Combine(work, Path.GetFileName(destination));
            var compressionProgress = new InlineProgress<ArchiveProgress>(value => progress?.Report(value with { Percent = value.Percent * 0.8 }));
            await engine.CompressAsync(request with { Destination = complete }, compressionProgress, cancellationToken);
            Directory.CreateDirectory(staging);
            await using (var input = new FileStream(complete, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var count = (input.Length - 1) / volumeSize + 1;
                if (count > 100000) throw new InvalidDataException("Too many split archive volumes. Choose a larger volume size.");
                var buffer = new byte[1024 * 1024];
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (var number = 1; input.Position < input.Length; number++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(destination) + "." + number.ToString("D3", CultureInfo.InvariantCulture);
                    await using var output = new FileStream(Path.Combine(staging, name), FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.Asynchronous);
                    var remaining = Math.Min(volumeSize, input.Length - input.Position);
                    while (remaining > 0)
                    {
                        var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
                        if (read == 0) throw new EndOfStreamException();
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        remaining -= read;
                        progress?.Report(new ArchiveProgress(80 + input.Position * 20d / input.Length, name, number - 1, (int)count, input.Position, input.Length, input.Position / Math.Max(watch.Elapsed.TotalSeconds, 0.001), watch.Elapsed));
                    }
                }
            }
            foreach (var part in Directory.GetFiles(staging).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.Combine(parent, Path.GetFileName(part));
                File.Move(part, target, overwrite: false);
                published.Add(target);
            }
            success = true;
            return destination + ".001";
        }
        finally
        {
            if (!success) foreach (var path in published) File.Delete(path);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.Delete(work, true);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
