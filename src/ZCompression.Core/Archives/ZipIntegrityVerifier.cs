using System.Diagnostics;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;

namespace ZCompression.Core.Archives;

public static class ZipIntegrityVerifier
{
    public static void Verify(string path, IProgress<ArchiveProgress>? progress = null, CancellationToken token = default, int? expectedCount = null, long? expectedBytes = null, string? password = null)
    {
        token.ThrowIfCancellationRequested();
        if (!string.IsNullOrEmpty(password))
        {
            using var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(path) { Password = password };
            var items = zip.Cast<ICSharpCode.SharpZipLib.Zip.ZipEntry>().ToArray();
            var size = items.Sum(entry => entry.Size);
            if (expectedCount is { } countCheck && countCheck != items.Length || expectedBytes is { } byteCheck && byteCheck != size)
                throw new InvalidDataException("Encrypted ZIP entry count or size does not match selected sources.");
            var timer = Stopwatch.StartNew();
            var copyBuffer = new byte[1024 * 1024];
            long readTotal = 0;
            var lastUpdate = 0L;
            void Update(string name, int completed) => progress?.Report(new ArchiveProgress(90 + (size == 0 ? 9.5 : readTotal * 9.5 / size), name, completed, items.Length, readTotal, size, readTotal / Math.Max(timer.Elapsed.TotalSeconds, 0.001), timer.Elapsed, IsVerifying: true));
            Update(Path.GetFileName(path), 0);
            foreach (var entry in items)
            {
                token.ThrowIfCancellationRequested();
                using var content = zip.GetInputStream(entry);
                long length = 0;
                var checksum = new Crc32();
                int read;
                while ((read = content.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    length += read; readTotal += read;
                    checksum.Append(copyBuffer.AsSpan(0, read));
                    if (timer.ElapsedMilliseconds - lastUpdate >= 100) { Update(entry.Name, 0); lastUpdate = timer.ElapsedMilliseconds; }
                }
                // AES AE-2 entries authenticate their payload instead of storing a CRC.
                if (length != entry.Size || (entry.Crc != 0 && checksum.GetCurrentHashAsUInt32() != (uint)entry.Crc))
                    throw new InvalidDataException("Encrypted ZIP integrity verification failed: " + entry.Name);
            }
            token.ThrowIfCancellationRequested();
            Update(Path.GetFileName(path), items.Length);
            return;
        }
        using var input = File.OpenRead(path);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, false, System.Text.Encoding.UTF8);
        var entries = archive.Entries;
        var total = entries.Sum(entry => entry.Length);
        if (expectedCount is { } count && count != entries.Count || expectedBytes is { } bytes && bytes != total)
            throw new InvalidDataException("ZIP entry count or size does not match the selected sources.");
        var buffer = new byte[1024 * 1024];
        var watch = Stopwatch.StartNew();
        var crc = new Crc32();
        long processed = 0;
        var completed = 0;
        var lastReport = 0L;
        void Report(string name) => progress?.Report(new ArchiveProgress(90 + (total == 0 ? 9.5 : processed * 9.5 / total), name, completed, entries.Count, processed, total, processed / Math.Max(watch.Elapsed.TotalSeconds, 0.001), watch.Elapsed, IsVerifying: true));
        Report(Path.GetFileName(path));
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            crc.Reset();
            long length = 0;
            using var content = entry.Open();
            int read;
            while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                crc.Append(buffer.AsSpan(0, read));
                length += read; processed += read;
                if (length > entry.Length) throw new InvalidDataException($"ZIP entry length is invalid: {entry.FullName}");
                if (watch.ElapsedMilliseconds - lastReport >= 100) { Report(entry.FullName); lastReport = watch.ElapsedMilliseconds; }
            }
            if (length != entry.Length || crc.GetCurrentHashAsUInt32() != entry.Crc32)
                throw new InvalidDataException($"ZIP CRC verification failed: {entry.FullName}");
            completed++;
        }
        token.ThrowIfCancellationRequested();
        Report(Path.GetFileName(path));
    }
}
