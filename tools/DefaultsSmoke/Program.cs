using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using ZCompression.App;
using ZCompression.Core.Archives;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent(); // Do not run app startup, file associations, or real user settings.
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exit = 1;
        _ = RunAsync().ContinueWith(task => dispatcher.BeginInvoke(new Action(() =>
        {
            if (task.IsCompletedSuccessfully) exit = 0;
            else Console.Error.WriteLine(task.Exception?.GetBaseException());
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        })), TaskScheduler.Default);
        Dispatcher.Run();
        return exit;
    }

    private static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "z-compression-defaults-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var localization = new JsonLocalizationService(Path.Combine(AppContext.BaseDirectory, "Localization"), "ko-KR");
            LocalizationManager.Instance.Initialize(localization);
            var settingsService = new JsonSettingsService(Path.Combine(root, "settings.json"));
            var source = Path.Combine(root, "source.txt");
            await File.WriteAllTextAsync(source, "test");
            var engine = new RecordingEngine();
            var vm = new MainViewModel(engine, localization, settingsService, new AppSettings());
            var settingsWindow = new SettingsWindow(vm);
            var levels = (ComboBox)settingsWindow.FindName("DefaultLevelBox");
            var formats = (ComboBox)settingsWindow.FindName("DefaultFormatBox");
            formats.SelectedItem = formats.Items.Cast<ComboBoxItem>().Single(item => Equals(item.Tag, "SevenZip"));
            foreach (var level in Enum.GetValues<CompressionPreset>())
            {
                levels.SelectedItem = levels.Items.Cast<ComboBoxItem>().Single(item => Equals(item.Tag, level.ToString()));
                await vm.SaveSettingsAsync();
                var saved = await settingsService.LoadAsync();
                if (saved.GetDefaultCompressionLevel() != level || saved.GetDefaultArchiveFormat() != ArchiveFormat.SevenZip) throw new Exception("Defaults were not persisted.");
                var reopened = new SettingsWindow(new MainViewModel(engine, localization, settingsService, saved));
                if (((ComboBox)reopened.FindName("DefaultLevelBox")).SelectedItem is not ComboBoxItem { Tag: string savedTag } || savedTag != level.ToString()) throw new Exception("Settings selection did not reload.");
                var dialog = new NewArchiveWindow([source], saved);
                if (((ComboBox)dialog.FindName("LevelBox")).SelectedItem is not ComboBoxItem { Tag: string tag } || tag != level.ToString() || dialog.SelectedLevel != level || dialog.SelectedFormat != ArchiveFormat.SevenZip) throw new Exception("New archive did not inherit defaults.");
                if (((TextBox)dialog.FindName("FileNameBox")).Text != "source.txt.7z") throw new Exception("Default format changed source naming.");
                await vm.QuickCompressAsync([source]);
                if (engine.LastCompression?.Level != level || engine.LastCompression.Format != ArchiveFormat.SevenZip) throw new Exception("Quick compression ignored defaults.");
                await vm.CompressAsync([source], Path.Combine(root, "archive.zip"), ArchiveFormat.Zip);
                if (engine.LastCompression?.Level != level) throw new Exception("Default compression request ignored settings.");
                await vm.AddToCurrentArchiveAsync([source]);
                if (engine.LastUpdate?.Level != level) throw new Exception("Archive update ignored defaults.");
                await vm.CompressAsync([source], Path.Combine(root, "override.zip"), ArchiveFormat.Zip, CompressionPreset.Store);
                if (engine.LastCompression?.Level != CompressionPreset.Store || vm.Settings.GetDefaultCompressionLevel() != level) throw new Exception("Per-operation override changed saved defaults.");
                dialog.Close(); reopened.Close();
            }
            settingsWindow.Close();
            Console.WriteLine("PASS: all 6 levels persist/reload, initialize new archive UI, reach quick compression and updates, and preserve per-operation overrides.");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class RecordingEngine : IArchiveEngine
    {
        public CompressionRequest? LastCompression { get; private set; }
        public ArchiveUpdateRequest? LastUpdate { get; private set; }
        public IReadOnlyDictionary<ArchiveFormat, ArchiveCapabilities> Capabilities => new Dictionary<ArchiveFormat, ArchiveCapabilities>();
        public Task CompressAsync(CompressionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
        { LastCompression = request; File.WriteAllText(request.Destination, "recorded compression"); return Task.CompletedTask; }
        public Task UpdateAsync(ArchiveUpdateRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
        { LastUpdate = request; return Task.CompletedTask; }
        public Task<IReadOnlyList<ArchiveEntryInfo>> ListAsync(string path, string? password = null, System.Text.Encoding? legacyEncoding = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArchiveEntryInfo>>([]);
        public Task ExtractAsync(ExtractionRequest request, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ExtractEntryAsync(string path, string entry, string destination, string? password = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TestAsync(string path, string? password = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
