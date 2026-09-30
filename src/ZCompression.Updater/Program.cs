using System.Diagnostics;

if (args.Length != 4 || !int.TryParse(args[0], out var processId))
{
    Console.Error.WriteLine("Usage: ZCompression.Updater <process-id> <package-directory> <install-directory> <executable-name>");
    return 2;
}

var packageDirectory = Path.GetFullPath(args[1]);
var installDirectory = Path.GetFullPath(args[2]);
var executableName = Path.GetFileName(args[3]);
try
{
    var process = Process.GetProcessById(processId);
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
}
catch (ArgumentException) { }

var backupDirectory = installDirectory.TrimEnd(Path.DirectorySeparatorChar) + ".backup";
try
{
    if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
    CopyDirectory(installDirectory, backupDirectory, overwrite: true);
    CopyDirectory(packageDirectory, installDirectory, overwrite: true);
    Process.Start(new ProcessStartInfo(Path.Combine(installDirectory, executableName)) { UseShellExecute = true });
    Directory.Delete(backupDirectory, true);
    Directory.Delete(packageDirectory, true);
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine("Update could not be installed. The previous version will be restored.");
    if (Directory.Exists(backupDirectory)) CopyDirectory(backupDirectory, installDirectory, overwrite: true);
    return 1;
}

static void CopyDirectory(string source, string destination, bool overwrite)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite);
    }
}
