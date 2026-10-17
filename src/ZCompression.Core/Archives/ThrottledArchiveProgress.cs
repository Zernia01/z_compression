using System.Diagnostics;

namespace ZCompression.Core.Archives;

internal sealed class ThrottledArchiveProgress(IProgress<ArchiveProgress> target) : IProgress<ArchiveProgress>
{
    private long _last;
    private bool _reported;
    public void Report(ArchiveProgress value)
    {
        var now = Stopwatch.GetTimestamp();
        if (_reported && value.CompletedFiles != value.TotalFiles && Stopwatch.GetElapsedTime(_last, now).TotalMilliseconds < 100) return;
        _reported = true; _last = now;
        target.Report(value);
    }
}
