using System.Diagnostics;

namespace ZCompression.Core.Archives;

/// <summary>Uses the user's separately installed RAR tool; it is never redistributed.</summary>
public static class RarCommandLine
{
    public static string? FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("Z_COMPRESSION_RAR_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? Path.GetFullPath(configured) : null;

        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetEnvironmentVariable("ProgramW6432") })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var candidate = Path.Combine(root, "WinRAR", "Rar.exe");
            if (File.Exists(candidate)) return candidate;
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), OperatingSystem.IsWindows() ? "rar.exe" : "rar");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }

    internal static void Compress(CompressionRequest request, IProgress<ArchiveProgress>? progress, CancellationToken token, string? executablePath)
    {
        token.ThrowIfCancellationRequested();
        var executable = executablePath ?? FindExecutable();
        if (executable is null || !File.Exists(executable))
            throw new NotSupportedException("RAR 압축에는 WinRAR의 rar.exe가 필요합니다. WinRAR를 설치하거나 Z_COMPRESSION_RAR_PATH에 rar.exe 경로를 지정해 주세요. / RAR creation requires WinRAR (rar.exe). Install it separately or set Z_COMPRESSION_RAR_PATH.");

        var destination = Path.GetFullPath(request.Destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporaryArchive = Path.Combine(Path.GetDirectoryName(destination)!, $".z-compression-{Guid.NewGuid():N}.rar");
        // Keep staging beside the destination, outside the selected source trees.
        // In particular, selecting the temporary folder must not copy staging into itself.
        var staging = Path.Combine(Path.GetDirectoryName(destination)!, $".z-compression-{Guid.NewGuid():N}.work");
        Directory.CreateDirectory(staging);
        var watch = Stopwatch.StartNew();
        try
        {
            // Stage only selected sources, preserving their root names and empty folders.
            // Relative input paths also avoid command/response-file interpretation of names.
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in request.Sources)
            {
                token.ThrowIfCancellationRequested();
                var fullSource = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var name = Path.GetFileName(fullSource);
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                    throw new IOException("RAR sources must have distinct, nonempty root names.");
                if (destination.Equals(fullSource, StringComparison.OrdinalIgnoreCase) ||
                    (Directory.Exists(fullSource) && destination.StartsWith(fullSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("The archive destination must be outside the selected sources.");
                CopySource(fullSource, Path.Combine(staging, name), token);
                progress?.Report(new ArchiveProgress(0, name, 0, request.Sources.Count, 0, 0, 0, watch.Elapsed));
            }

            var files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
            var bytes = files.Sum(path => new FileInfo(path).Length);
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                WorkingDirectory = staging,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            foreach (var argument in new[] { "a", "-ma5", "-r", "-y", "-idq", "-cfg-", "-m" + Level(request.Level) }) start.ArgumentList.Add(argument);
            // Ignore user rar.ini/RAR environment switches for predictable output.
            start.Environment.Remove("RAR");
            if (!string.IsNullOrEmpty(request.Password))
            {
                start.ArgumentList.Add("-hp" + request.Password);
            }
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(temporaryArchive);
            start.ArgumentList.Add("*");

            progress?.Report(new ArchiveProgress(0, Path.GetFileName(destination), 0, files.Length, 0, bytes, 0, watch.Elapsed));
            using var process = Process.Start(start) ?? throw new IOException("Could not start the RAR tool.");
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try { process.WaitForExitAsync(token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw;
            }
            var detail = (error.GetAwaiter().GetResult() + " " + output.GetAwaiter().GetResult()).Trim();
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(temporaryArchive))
                throw new IOException($"RAR creation failed (exit code {process.ExitCode}). {detail}");
            File.Move(temporaryArchive, destination, overwrite: true);
            progress?.Report(new ArchiveProgress(100, Path.GetFileName(destination), files.Length, files.Length, bytes, bytes, bytes / Math.Max(watch.Elapsed.TotalSeconds, 0.001), watch.Elapsed));
        }
        finally
        {
            if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            Directory.Delete(staging, recursive: true);
        }
    }

    private static void CopySource(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Source path was not found.", source);
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Symbolic links and junctions cannot be added to a RAR archive.");
        if (File.Exists(source))
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyToAsync(output, 1024 * 1024, token).GetAwaiter().GetResult();
            File.SetLastWriteTime(destination, File.GetLastWriteTime(source));
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (var child in Directory.EnumerateFileSystemEntries(source))
            CopySource(child, Path.Combine(destination, Path.GetFileName(child)), token);
        Directory.SetLastWriteTime(destination, Directory.GetLastWriteTime(source));
    }

    private static int Level(CompressionPreset level) => level switch
    {
        CompressionPreset.Store => 0,
        CompressionPreset.Fastest => 1,
        CompressionPreset.Fast => 2,
        CompressionPreset.Normal => 3,
        CompressionPreset.High => 4,
        CompressionPreset.Ultra => 5,
        _ => 3,
    };
}
