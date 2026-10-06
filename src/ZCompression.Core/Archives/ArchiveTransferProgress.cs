using System.Diagnostics;

namespace ZCompression.Core.Archives;

public sealed class ArchiveTransferProgress
{
    private readonly Dictionary<string, long> _sizes;
    private readonly Dictionary<string, long> _positions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly IProgress<ArchiveProgress>? _progress;
    private readonly Stopwatch _clock = new();
    private readonly object _gate = new();
    private readonly long _total;
    private long _processed;
    private long _lastReport = -100;

    public ArchiveTransferProgress(IReadOnlyList<ArchiveExportEntry> entries, IProgress<ArchiveProgress>? progress)
    {
        _sizes = entries.Where(entry => !entry.IsDirectory).ToDictionary(entry => entry.Name, entry => entry.Size, StringComparer.Ordinal);
        _total = _sizes.Values.Sum();
        _progress = progress;
    }

    public void Report(string name, long position, bool complete)
    {
        lock (_gate)
        {
            if (!_sizes.TryGetValue(name, out var size)) return;
            if (!_clock.IsRunning) _clock.Start();
            var previous = _positions.GetValueOrDefault(name);
            var current = Math.Clamp(position, previous, size);
            _positions[name] = current;
            _processed += current - previous;
            var newlyCompleted = complete && current == size && _completed.Add(name);
            // Small files must not flood the UI queue; always publish the final verified file.
            if (!(newlyCompleted && _completed.Count == _sizes.Count) && _clock.ElapsedMilliseconds - _lastReport < 100) return;
            _lastReport = _clock.ElapsedMilliseconds;
            var percent = _total == 0 ? _completed.Count * 100d / Math.Max(1, _sizes.Count) : _processed * 100d / _total;
            var speed = _processed / Math.Max(.001, _clock.Elapsed.TotalSeconds);
            _progress?.Report(new(percent, name, _completed.Count, _sizes.Count, _processed, _total, speed, _clock.Elapsed));
        }
    }
}
