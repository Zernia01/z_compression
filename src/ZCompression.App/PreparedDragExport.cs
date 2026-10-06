using System.IO;

namespace ZCompression.App;

// OLE receives completed files only; no extraction or UI dispatch occurs in GetData.
internal sealed class PreparedDragExport(string[] paths, Func<Task> prepare)
{
    private Task? _preparation;
    private bool _ready;
    internal bool IsReady => _ready && paths.All(path => File.Exists(path) || Directory.Exists(path));
    internal Task PrepareAsync() => _preparation ??= PrepareCoreAsync();

    private async Task PrepareCoreAsync()
    {
        await prepare();
        if (paths.Any(path => !File.Exists(path) && !Directory.Exists(path)))
            throw new FileNotFoundException("A selected archive item could not be extracted.");
        _ready = true;
    }

    internal string[] GetReadyPaths()
    {
        if (!IsReady) throw new InvalidOperationException("Archive files have not finished preparing.");
        return paths.ToArray();
    }
}
