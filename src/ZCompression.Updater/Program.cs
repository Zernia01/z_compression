using System.Diagnostics;
using System.Runtime.InteropServices;
using ZCompression.Update;

if (args.Length != 4 || !int.TryParse(args[0], out var processId)) return 2;
var packageDirectory = Path.GetFullPath(args[1]);
var installDirectory = Path.GetFullPath(args[2]);
var executableName = Path.GetFileName(args[3]);
try
{
    try
    {
        using var process = Process.GetProcessById(processId);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
    }
    catch (ArgumentException) { }
    await UpdateFileInstaller.ApplyAsync(packageDirectory, installDirectory, executableName);
    Process.Start(new ProcessStartInfo(Path.Combine(installDirectory, executableName)) { UseShellExecute = true });
    UpdateFileInstaller.TryDeleteDirectory(packageDirectory);
    var stagingRoot = Path.GetDirectoryName(packageDirectory)!;
    var updateRoot = Path.Combine(Path.GetTempPath(), "z_compression", "update") + Path.DirectorySeparatorChar;
    if (stagingRoot.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase))
        UpdateFileInstaller.TryDeleteDirectory(Path.Combine(stagingRoot, "download"));
    return 0;
}
catch (Exception exception)
{
    var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "z_compression", "logs", "update-error.log");
    try { Directory.CreateDirectory(Path.GetDirectoryName(log)!); File.AppendAllText(log, $"{DateTimeOffset.Now:u}\n{exception}\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    if (OperatingSystem.IsWindows()) Native.MessageBox(IntPtr.Zero, $"업데이트를 완료하지 못했습니다. 프로그램을 다시 실행하거나 설치 파일로 업데이트해 주세요.\n{exception.Message}\n\n로그: {log}", "z_compression 업데이트", 0x10);
    return 1;
}

internal static class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
