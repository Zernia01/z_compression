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
    Directory.Move(installDirectory, backupDirectory);
    Directory.Move(packageDirectory, installDirectory);
    Process.Start(new ProcessStartInfo(Path.Combine(installDirectory, executableName)) { UseShellExecute = true });
    Directory.Delete(backupDirectory, true);
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Update could not be installed. The previous version will be restored.");
    if (!Directory.Exists(installDirectory) && Directory.Exists(backupDirectory)) Directory.Move(backupDirectory, installDirectory);
    return 1;
}
