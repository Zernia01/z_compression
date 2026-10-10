using System.Windows;
using System.IO;
using System.Windows.Threading;
using ZCompression.Core.Archives;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;
using System.Diagnostics;

namespace ZCompression.App;

public partial class App : Application
{
    public App() => DispatcherUnhandledException += OnUnhandledException;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var args = e.Args;
        if (args.FirstOrDefault() is "--compress-here" or "--extract-here")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var collected = await ShellSelectionCollector.CollectAsync(args[0], args.Skip(1).ToArray());
                if (collected is null) { Shutdown(); return; }
                args = [args[0], .. collected];
            }
            catch (Exception exception) { MessageBox.Show(exception.Message, "z_compression"); Shutdown(1); return; }
        }
        CleanupOldPreviews();
        var settingsService = new JsonSettingsService();
        var settings = await settingsService.LoadAsync();
        ApplyProcessPriority(settings.OperationPriority);
        ThemeManager.Apply(settings.Theme);
        var localization = new JsonLocalizationService(Path.Combine(AppContext.BaseDirectory, "Localization"), settings.Language);
        LocalizationManager.Instance.Initialize(localization);
        try
        {
            if (Environment.ProcessPath is { } executable) FileAssociationService.RegisterCurrentUser(executable, settings);
        }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
        catch (IOException) { }
        if (e.Args.FirstOrDefault()?.Equals("--register-shell", StringComparison.OrdinalIgnoreCase) == true)
        {
            Shutdown();
            return;
        }
        var viewModel = new MainViewModel(new SharpCompressArchiveEngine(), localization, settingsService, settings);
        var command = e.Args.FirstOrDefault();
        if (command?.Equals("--compress-here", StringComparison.OrdinalIgnoreCase) == true ||
            command?.Equals("--extract-here", StringComparison.OrdinalIgnoreCase) == true)
        {
            await RunQuickOperationAsync(viewModel, args, command.Equals("--extract-here", StringComparison.OrdinalIgnoreCase));
            return;
        }
        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
        if (e.Args.FirstOrDefault()?.Equals("--compress", StringComparison.OrdinalIgnoreCase) == true)
        {
            var sources = e.Args.Skip(1).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
            await mainWindow.CreateArchiveFromSourcesAsync(sources);
            return;
        }
        if (e.Args.FirstOrDefault() is { } requestedPath && File.Exists(requestedPath) && MainViewModel.IsArchivePath(requestedPath))
        {
            try
            {
                await viewModel.OpenArchiveAsync(requestedPath);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"{LocalizationManager.Instance["OpenArchiveError"]}\n{exception.Message}", "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        if (e.Args.Length == 0 && settings.CheckForUpdatesAtStartup)
            await UpdateCoordinator.CheckAndInstallAsync(mainWindow, showUpToDateMessage: false);
    }

    private async Task RunQuickOperationAsync(MainViewModel viewModel, string[] args, bool extract)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var sources = args.Skip(1).ToArray();
        var operation = LocalizationManager.Instance[extract ? "ExtractAction" : "CompressAction"];
        var progressWindow = new OperationProgressWindow(viewModel, operation, Path.GetFileName(sources.FirstOrDefault() ?? ""), extract);
        MainWindow = progressWindow;
        progressWindow.Show();
        viewModel.RequestArchivePassword = path =>
        {
            var dialog = new ArchivePasswordWindow(path) { Owner = progressWindow };
            return dialog.ShowDialog() == true ? dialog.Password : null;
        };
        var exitCode = 0;
        try
        {
            if (extract)
            {
                await viewModel.ExtractManyAsync(sources);
            }
            else await viewModel.QuickCompressAsync(sources);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            exitCode = 1;
            MessageBox.Show(progressWindow, viewModel.FriendlyError(exception), "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            progressWindow.Finish();
            Shutdown(exitCode);
        }
    }

    internal static void ApplyProcessPriority(string priority)
    {
        try
        {
            Process.GetCurrentProcess().PriorityClass = priority.ToLowerInvariant() switch
            {
                "low" => ProcessPriorityClass.BelowNormal,
                "high" => ProcessPriorityClass.AboveNormal,
                _ => ProcessPriorityClass.Normal,
            };
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void CleanupOldPreviews()
    {
        foreach (var category in new[] { "preview", "drag" })
        {
            var root = Path.Combine(Path.GetTempPath(), "z_compression", category);
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    if (Directory.GetCreationTimeUtc(directory) < DateTime.UtcNow.AddDays(-2)) Directory.Delete(directory, true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "z_compression", "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "error.log"), $"{DateTimeOffset.Now:u} {e.Exception.GetType().Name} (0x{e.Exception.HResult:X8}){Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        MessageBox.Show(LocalizationManager.Instance["UnexpectedErrorLog"], "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
