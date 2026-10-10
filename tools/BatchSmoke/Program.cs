using System.IO;
using System.Windows.Threading;
using ZCompression.App;
using ZCompression.Core.Archives;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--send")
        {
            try { return ShellSelectionCollector.CollectAsync("compress", [args[2], "one"], args[1]).GetAwaiter().GetResult() is null ? 0 : 2; }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        }
        var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
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
        var pipe = "z-compression-test-" + Guid.NewGuid().ToString("N");
        var owner = ShellSelectionCollector.CollectAsync("compress", ["한글 folder", "one"], pipe);
        var clients = Enumerable.Range(0, 5).Select(index =>
        {
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--send"); start.ArgumentList.Add(pipe); start.ArgumentList.Add("item " + index);
            return System.Diagnostics.Process.Start(start)!;
        }).ToArray();
        await Task.WhenAll(clients.Select(client => client.WaitForExitAsync()));
        if (clients.Any(client => client.ExitCode != 0)) throw new Exception("A secondary shell process failed or started an operation.");
        foreach (var client in clients) client.Dispose();
        var collected = await owner;
        if (collected?.Length != 7 || !collected.Contains("한글 folder")) throw new Exception("Shell selection grouping lost or duplicated paths.");
        var next = await ShellSelectionCollector.CollectAsync("compress", ["next operation"], pipe);
        if (next?.Single() != "next operation") throw new Exception("Collector was not released after the batch.");

        var root = Path.Combine(Path.GetTempPath(), "z-compression-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var engine = new SharpCompressArchiveEngine();
            var archives = new List<string>();
            foreach (var format in new[] { ArchiveFormat.Zip, ArchiveFormat.SevenZip, ArchiveFormat.TarGZip })
            {
                var folder = Directory.CreateDirectory(Path.Combine(root, format.ToString())).FullName;
                var source = Path.Combine(folder, "한글.txt");
                await File.WriteAllTextAsync(source, format.ToString());
                archives.Add(await new QuickArchiveService(engine).CompressAsync([source], format));
            }
            var localization = new JsonLocalizationService(Path.Combine(AppContext.BaseDirectory, "Localization"), "ko-KR");
            LocalizationManager.Instance.Initialize(localization);
            var vm = new MainViewModel(engine, localization, new JsonSettingsService(Path.Combine(root, "settings.json")), new AppSettings());
            var destination = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;
            await vm.ExtractManyAsync([.. archives, archives[0]], destination);
            foreach (var format in new[] { ArchiveFormat.Zip, ArchiveFormat.SevenZip, ArchiveFormat.TarGZip })
            {
                var output = Path.Combine(destination, "한글", "한글.txt");
                if (format == ArchiveFormat.SevenZip) output = Path.Combine(destination, "한글 (2)", "한글.txt");
                if (format == ArchiveFormat.TarGZip) output = Path.Combine(destination, "한글 (3)", "한글.txt");
                if (await File.ReadAllTextAsync(output) != format.ToString()) throw new Exception("Batch extraction lost content or overwrote a result.");
            }
            if (vm.IsBusy || vm.ProgressPercent != 100 || Directory.GetDirectories(destination).Length != 3) throw new Exception("Batch completion or deduplication failed.");
            await vm.ExtractManyAsync(archives);
            foreach (var archive in archives)
                if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(archive)!, "한글"))) throw new Exception("Shell batch did not extract beside each archive.");
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == "Status" && vm.Status.Contains("(2/3)", StringComparison.Ordinal)) vm.Cancel(); };
            var canceled = Path.Combine(root, "canceled");
            try { await vm.ExtractManyAsync(archives, canceled); throw new Exception("Batch cancellation was ignored."); }
            catch (OperationCanceledException) { }
            if (vm.IsBusy || Directory.GetDirectories(canceled).Length != 1) throw new Exception("Canceled batch continued or left partial output.");
            if (Directory.GetFileSystemEntries(canceled, ".z-compression-*").Length != 0) throw new Exception("Canceled batch left staging output.");
            Console.WriteLine("PASS: simultaneous shell selection grouping, deduplication, next batch, ZIP/7Z/TAR.GZ batch extraction, name collision preservation, chosen destination and per-archive destinations.");
        }
        finally { Directory.Delete(root, true); }
    }
}
