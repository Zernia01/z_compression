namespace ZCompression.Update;

public static class UpdateFileInstaller
{
    public static async Task ApplyAsync(string packageDirectory, string installDirectory, string executableName, CancellationToken token = default)
    {
        var package = Path.GetFullPath(packageDirectory);
        var install = Path.GetFullPath(installDirectory);
        if (!File.Exists(Path.Combine(package, Path.GetFileName(executableName)))) throw new InvalidDataException("The update package is missing the application executable.");
        if (package.Equals(install, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package and installation directories must differ.");
        var files = Directory.GetFiles(package, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(package, path)).Order(StringComparer.Ordinal).ToArray();
        var backup = Path.Combine(Path.GetTempPath(), "z_compression", "update-backups", Guid.NewGuid().ToString("N"));
        var originals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var touched = new List<string>();
        var preserveBackup = false;
        try
        {
            // Finish every backup before changing any installed file.
            foreach (var relative in files)
            {
                var target = Path.Combine(install, relative);
                if (!File.Exists(target)) continue;
                await CopyWithRetryAsync(target, Path.Combine(backup, relative), token);
                originals.Add(relative);
            }
            try
            {
                foreach (var relative in files)
                {
                    await InstallFileAsync(Path.Combine(package, relative), Path.Combine(install, relative), token);
                    touched.Add(relative);
                }
            }
            catch (Exception failure)
            {
                var rollbackErrors = new List<Exception>();
                foreach (var relative in touched.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (originals.Contains(relative)) await InstallFileAsync(Path.Combine(backup, relative), Path.Combine(install, relative), CancellationToken.None);
                        else if (File.Exists(Path.Combine(install, relative))) File.Delete(Path.Combine(install, relative));
                    }
                    catch (Exception rollback) { rollbackErrors.Add(rollback); }
                }
                if (rollbackErrors.Count > 0)
                {
                    preserveBackup = true;
                    throw new AggregateException($"Update rollback failed. Backups: {backup}", new[] { failure }.Concat(rollbackErrors));
                }
                throw;
            }
        }
        finally
        {
            // Cleanup failures must never roll back an already installed update.
            if (!preserveBackup) TryDeleteDirectory(backup);
        }
    }

    private static async Task CopyWithRetryAsync(string source, string destination, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await ActionWithRetryAsync(() => File.Copy(source, destination, true), token);
    }

    private static async Task InstallFileAsync(string source, string destination, CancellationToken token)
    {
        var staged = destination + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            await CopyWithRetryAsync(source, staged, token);
            await ActionWithRetryAsync(() =>
            {
                if (File.Exists(destination)) File.Replace(staged, destination, null);
                else File.Move(staged, destination);
            }, token);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }

    private static async Task ActionWithRetryAsync(Action action, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { action(); return; }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33 && attempt < 25)
            { await Task.Delay(200, token); }
        }
    }

    public static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
