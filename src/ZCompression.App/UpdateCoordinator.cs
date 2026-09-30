using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using ZCompression.Update;

namespace ZCompression.App;

internal static class UpdateCoordinator
{
    internal static async Task CheckAndInstallAsync(Window owner, bool showUpToDateMessage)
    {
        try
        {
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var service = new GitHubUpdateService(httpClient, GitHubUpdateService.DefaultOwner, GitHubUpdateService.DefaultRepository);
            var result = await service.CheckAsync(currentVersion);
            if (!result.IsUpdateAvailable)
            {
                if (showUpToDateMessage)
                    MessageBox.Show(owner, LocalizationManager.Instance["UpdateCurrent"], LocalizationManager.Instance["AutomaticUpdates"], MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (result.Manifest is null)
                throw new InvalidDataException("The release does not contain an update manifest.");

            var prompt = string.Format(LocalizationManager.Instance["UpdateAvailable"], result.LatestVersion);
            if (MessageBox.Show(owner, prompt, LocalizationManager.Instance["AutomaticUpdates"], MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes) != MessageBoxResult.Yes)
                return;

            var stagingRoot = Path.Combine(Path.GetTempPath(), "z_compression", "update", Guid.NewGuid().ToString("N"));
            var downloadDirectory = Path.Combine(stagingRoot, "download");
            var packageDirectory = Path.Combine(stagingRoot, "package");
            var runnerDirectory = Path.Combine(stagingRoot, "runner");
            var archivePath = await service.DownloadAndVerifyAsync(result.Manifest, downloadDirectory);
            Directory.CreateDirectory(packageDirectory);
            ZipFile.ExtractToDirectory(archivePath, packageDirectory, overwriteFiles: true);

            var updaterName = "ZCompression.Updater.exe";
            var packagedUpdater = Path.Combine(packageDirectory, updaterName);
            if (!File.Exists(packagedUpdater)) throw new FileNotFoundException("The update package does not contain the updater.", packagedUpdater);
            Directory.CreateDirectory(runnerDirectory);
            var runnerPath = Path.Combine(runnerDirectory, updaterName);
            File.Copy(packagedUpdater, runnerPath, overwrite: true);

            var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The application path is unavailable.");
            var startInfo = new ProcessStartInfo(runnerPath) { UseShellExecute = false };
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(packageDirectory);
            startInfo.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            startInfo.ArgumentList.Add(Path.GetFileName(executablePath));
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            if (showUpToDateMessage)
                MessageBox.Show(owner, $"{LocalizationManager.Instance["UpdateFailed"]}\n{exception.Message}", LocalizationManager.Instance["AutomaticUpdates"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
