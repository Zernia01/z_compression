using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using ZCompression.Core.Archives;
using ZCompression.Core.Collections;
using ZCompression.Core.Localization;
using ZCompression.Core.Settings;

namespace ZCompression.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IArchiveEngine _engine;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _cancellation;
    private string _searchText = "";
    private string _status = "";
    private string _currentArchive = "";
    private string _currentArchivePath = "";
    private double _progressPercent;
    private string _progressDetails = "";
    private string _currentOperationFile = "";
    private string _operationCounts = "";
    private string _processedText = "";
    private string _speedText = "";
    private string _elapsedText = "";
    private string _remainingText = "";
    private bool _isBusy;
    private ArchiveEntryInfo? _selectedEntry;
    private ArchiveBrowserItem? _selectedBrowserItem;
    private string _currentFolder = "";
    private readonly Dictionary<string, string> _archivePasswords = new(StringComparer.OrdinalIgnoreCase);
    public Func<string, string?>? RequestArchivePassword { get; set; }

    public MainViewModel(IArchiveEngine engine, ILocalizationService localization, ISettingsService settingsService, AppSettings settings)
    {
        _engine = engine; _localization = localization; _settingsService = settingsService; Settings = settings;
        _localization.LanguageChanged += (_, _) => NotifyLocalized();
        Status = _localization["Ready"];
    }

    public BulkObservableCollection<ArchiveEntryInfo> Entries { get; } = [];
    public BulkObservableCollection<ArchiveBrowserItem> BrowserItems { get; } = [];
    public AppSettings Settings { get; private set; }
    public string Subtitle => _localization["Subtitle"];
    public string NewArchiveText => _localization["NewArchive"];
    public string ExtractText => _localization["Extract"];
    public string OpenText => _localization["Open"];
    public string TestText => _localization["Test"];
    public string SettingsText => _localization["Settings"];
    public string DropText => _localization["Drop"];
    public string DropHint => _localization["DropHint"];
    public string CancelText => _localization["Cancel"];
    public string CurrentArchive { get => _currentArchive; private set => Set(ref _currentArchive, value); }
    public string CurrentArchivePath { get => _currentArchivePath; private set => Set(ref _currentArchivePath, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public double ProgressPercent { get => _progressPercent; private set => Set(ref _progressPercent, value); }
    public string ProgressDetails { get => _progressDetails; private set => Set(ref _progressDetails, value); }
    public string CurrentOperationFile { get => _currentOperationFile; private set => Set(ref _currentOperationFile, value); }
    public string OperationCounts { get => _operationCounts; private set => Set(ref _operationCounts, value); }
    public string ProcessedText { get => _processedText; private set => Set(ref _processedText, value); }
    public string SpeedText { get => _speedText; private set => Set(ref _speedText, value); }
    public string ElapsedText { get => _elapsedText; private set => Set(ref _elapsedText, value); }
    public string RemainingText { get => _remainingText; private set => Set(ref _remainingText, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public bool HasArchive => !string.IsNullOrWhiteSpace(CurrentArchive);
    public bool CanModifyCurrentArchive => HasArchive && _engine.Capabilities.TryGetValue(FormatFromPath(CurrentArchivePath), out var capabilities) && capabilities.CanModify;
    public ArchiveEntryInfo? SelectedEntry { get => _selectedEntry; set => Set(ref _selectedEntry, value); }
    public ArchiveBrowserItem? SelectedBrowserItem
    {
        get => _selectedBrowserItem;
        set { if (Set(ref _selectedBrowserItem, value)) SelectedEntry = value?.Entry; }
    }
    public string CurrentFolder { get => _currentFolder; private set { if (Set(ref _currentFolder, value)) { OnPropertyChanged(nameof(BreadcrumbText)); OnPropertyChanged(nameof(CanNavigateUp)); } } }
    public string BreadcrumbText => string.IsNullOrEmpty(CurrentFolder) ? CurrentArchive : $"{CurrentArchive}  ›  {CurrentFolder.Replace('/', '›')}";
    public bool CanNavigateUp => !string.IsNullOrEmpty(CurrentFolder);
    public int VisibleFolderCount => BrowserItems.Count(item => item.IsDirectory && !item.IsParent);
    public int VisibleFileCount => BrowserItems.Count(item => !item.IsDirectory);
    public string BrowserStatus => string.Format(_localization["BrowserStatus"], VisibleFileCount, VisibleFolderCount, Entries.Count);
    public string BrowserItemCountText => string.Format(_localization["ItemCount"], BrowserItems.Count);
    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) RebuildBrowserItems(); } }

    public async Task OpenArchiveAsync(string path) => await WithOperation(async token =>
    {
        Status = _localization["Opening"];
        var entries = await WithArchivePassword(path, password => _engine.ListAsync(path, password, cancellationToken: token));
        Entries.ReplaceAll(entries);
        CurrentArchivePath = Path.GetFullPath(path); CurrentArchive = Path.GetFileName(path); CurrentFolder = ""; SelectedEntry = null; SelectedBrowserItem = null; RebuildBrowserItems(); Status = string.Format(_localization["EntryCount"], entries.Count);
    });

    public async Task CompressAsync(IReadOnlyList<string> sources, string destination, ArchiveFormat format, CompressionPreset level = CompressionPreset.Normal, string? password = null) => await WithOperation(async token =>
    {
        Status = _localization["Compressing"];
        await _engine.CompressAsync(new CompressionRequest(sources, destination, format, level, password), CreateProgress(), token);
        var entries = await _engine.ListAsync(destination, password, cancellationToken: token);
        var fullPath = Path.GetFullPath(destination);
        _archivePasswords.Remove(fullPath);
        if (password is not null) _archivePasswords[fullPath] = password;
        Entries.ReplaceAll(entries);
        CurrentArchivePath = Path.GetFullPath(destination); CurrentArchive = Path.GetFileName(destination);
        CurrentFolder = ""; SelectedEntry = null; SelectedBrowserItem = null; RebuildBrowserItems();
        Status = _localization["CompressionComplete"];
    });

    public async Task ExtractAsync(string archive, string destination) => await WithOperation(async token =>
    {
        Status = _localization["Extracting"];
        await WithArchivePassword(archive, password => _engine.ExtractAsync(new ExtractionRequest(archive, destination, password), CreateProgress(), token));
        Status = _localization["ExtractionComplete"];
    });

    public async Task AddToCurrentArchiveAsync(IReadOnlyList<string> sources) => await WithOperation(async token =>
    {
        if (string.IsNullOrWhiteSpace(CurrentArchivePath)) throw new InvalidOperationException("No archive is open.");
        Status = _localization["Compressing"];
        var format = FormatFromPath(CurrentArchivePath);
        await WithArchivePassword(CurrentArchivePath, password => _engine.UpdateAsync(new ArchiveUpdateRequest(CurrentArchivePath, sources, format, CurrentFolder, CompressionPreset.High, password), CreateProgress(), token));
        var entries = await WithArchivePassword(CurrentArchivePath, password => _engine.ListAsync(CurrentArchivePath, password, cancellationToken: token));
        Entries.ReplaceAll(entries);
        SelectedEntry = null; SelectedBrowserItem = null; RebuildBrowserItems();
        Status = _localization["CompressionComplete"];
    });

    public async Task TestAsync(string archive) => await WithOperation(async token =>
    {
        Status = _localization["Testing"];
        await WithArchivePassword(archive, password => _engine.TestAsync(archive, password, cancellationToken: token));
        Status = _localization["ArchiveHealthy"];
    });

    public void Cancel() => _cancellation?.Cancel();

    public async Task QuickCompressAsync(IReadOnlyList<string> sources) => await WithOperation(async token =>
    {
        Status = _localization["Compressing"];
        var format = Settings.DefaultArchiveFormat.ToLowerInvariant() switch
        {
            "7z" or "sevenzip" => ArchiveFormat.SevenZip, "rar" => ArchiveFormat.Rar,
            "tar" => ArchiveFormat.Tar, "tar.gz" or "targzip" => ArchiveFormat.TarGZip, _ => ArchiveFormat.Zip,
        };
        var level = Enum.TryParse<CompressionPreset>(Settings.DefaultCompressionLevel, true, out var preset) ? preset : CompressionPreset.High;
        await new QuickArchiveService(_engine).CompressAsync(sources, format, level, CreateProgress(), token);
        Status = _localization["CompressionComplete"];
    });

    public async Task QuickExtractAsync(string archive) => await WithOperation(async token =>
    {
        Status = _localization["Extracting"];
        await WithArchivePassword(archive, password => new QuickArchiveService(_engine).ExtractAsync(archive, password, CreateProgress(), token));
        Status = _localization["ExtractionComplete"];
    });
    public void ClearArchive()
    {
        _archivePasswords.Clear();
        Entries.ReplaceAll([]); BrowserItems.ReplaceAll([]); SelectedEntry = null; SelectedBrowserItem = null; CurrentFolder = ""; CurrentArchivePath = string.Empty; CurrentArchive = string.Empty; SearchText = string.Empty; Status = _localization["Ready"];
    }

    public void OpenBrowserItem(ArchiveBrowserItem? item)
    {
        if (item is null || !item.IsDirectory) return;
        if (item.IsParent) NavigateUp();
        else
        {
            CurrentFolder = string.IsNullOrEmpty(CurrentFolder) ? item.Name : $"{CurrentFolder}/{item.Name}";
            SelectedBrowserItem = null;
            RebuildBrowserItems();
        }
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentFolder)) return;
        var index = CurrentFolder.LastIndexOf('/');
        CurrentFolder = index < 0 ? "" : CurrentFolder[..index];
        SelectedBrowserItem = null;
        RebuildBrowserItems();
    }

    public async Task<string> ExtractForOpenAsync(ArchiveBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsDirectory || item.Entry is null) throw new InvalidOperationException("Only files can be opened.");
        if (string.IsNullOrWhiteSpace(CurrentArchivePath)) throw new InvalidOperationException("No archive is open.");

        var previewDirectory = Path.Combine(Path.GetTempPath(), "z_compression", "preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(previewDirectory);
        var sourceName = Path.GetFileName(item.Name);
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = new string(sourceName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file";
        var destination = Path.Combine(previewDirectory, "preview-" + safeName);

        await WithOperation(async token =>
        {
            Status = string.Format(_localization["OpeningFile"], item.Name);
            await WithArchivePassword(CurrentArchivePath, password => _engine.ExtractEntryAsync(CurrentArchivePath, item.Path, destination, password, token));
            Status = _localization["Ready"];
        });
        return destination;
    }

    public static bool IsPotentiallyExecutable(string name)
    {
        var extension = Path.GetExtension(name);
        return new[] { ".exe", ".com", ".bat", ".cmd", ".ps1", ".msi", ".msp", ".scr", ".vbs", ".js", ".jse", ".wsf", ".lnk", ".reg" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
    public void SetLanguage(string language) { Settings = Settings with { Language = language }; _localization.SetCulture(language); }
    public void SetTheme(string theme) => Settings = Settings with { Theme = theme };
    public void SetCpuThreads(int threads) => Settings = Settings with { CpuThreads = Math.Max(0, threads) };
    public void SetOperationPriority(string priority) => Settings = Settings with { OperationPriority = priority };
    public void SetCheckForUpdatesAtStartup(bool enabled) => Settings = Settings with { CheckForUpdatesAtStartup = enabled };
    public void SetShortcuts(string compressShortcut, string extractShortcut) => Settings = Settings with
    {
        CompressShortcut = compressShortcut,
        ExtractShortcut = extractShortcut,
    };
    public Task SaveSettingsAsync() => _settingsService.SaveAsync(Settings);

    public string FriendlyError(Exception exception) => exception switch
    {
        InvalidDataException => _localization["InvalidArchive"],
        UnauthorizedAccessException => _localization["AccessDenied"],
        NotSupportedException => exception.Message,
        _ => _localization["UnexpectedError"],
    };

    public static bool IsArchivePath(string path) => new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static ArchiveFormat FormatFromPath(string path) => path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ? ArchiveFormat.TarGZip : Path.GetExtension(path).ToLowerInvariant() switch { ".7z" => ArchiveFormat.SevenZip, ".rar" => ArchiveFormat.Rar, ".tar" => ArchiveFormat.Tar, ".gz" => ArchiveFormat.GZip, ".bz2" => ArchiveFormat.BZip2, ".xz" => ArchiveFormat.Xz, ".zst" => ArchiveFormat.Zstandard, _ => ArchiveFormat.Zip };

    private Progress<ArchiveProgress> CreateProgress() => new(value =>
    {
        ProgressPercent = value.Percent;
        CurrentOperationFile = value.CurrentFile;
        OperationCounts = string.Format(_localization["ProgressCount"], value.CompletedFiles, value.TotalFiles);
        ProcessedText = $"{FormatBytes(value.ProcessedBytes)} / {FormatBytes(value.TotalBytes)}";
        SpeedText = $"{FormatBytes(value.BytesPerSecond)}/s";
        ElapsedText = FormatDuration(value.Elapsed);
        var remaining = value.Percent > 0 && value.Percent < 100
            ? TimeSpan.FromTicks((long)(value.Elapsed.Ticks * (100d - value.Percent) / value.Percent)) : TimeSpan.Zero;
        RemainingText = remaining > TimeSpan.Zero ? FormatDuration(remaining) : _localization["Calculating"];
        ProgressDetails = $"{OperationCounts} · {SpeedText} · {value.CurrentFile}";
    });
    private void RebuildBrowserItems()
    {
        var items = new List<ArchiveBrowserItem>();
        if (!string.IsNullOrEmpty(CurrentFolder)) items.Add(new ArchiveBrowserItem("..", CurrentFolder, true, true, 0, 0, null, null, null));

        var prefix = string.IsNullOrEmpty(CurrentFolder) ? "" : CurrentFolder.Trim('/') + "/";
        var folders = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var entry in Entries)
        {
            var normalized = entry.Path.Replace('\\', '/').TrimStart('/');
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var relative = normalized[prefix.Length..].TrimEnd('/');
            if (string.IsNullOrEmpty(relative)) continue;
            var separator = relative.IndexOf('/');
            if (separator >= 0)
            {
                var folder = relative[..separator];
                if (MatchesSearch(folder) && folders.Add(folder)) items.Add(new ArchiveBrowserItem(folder, prefix + folder, true, false, 0, 0, entry.Modified, null, null));
                continue;
            }
            if (entry.IsDirectory)
            {
                if (MatchesSearch(relative) && folders.Add(relative)) items.Add(new ArchiveBrowserItem(relative, normalized, true, false, 0, 0, entry.Modified, entry.Checksum, entry));
            }
            else if (MatchesSearch(relative))
            {
                items.Add(new ArchiveBrowserItem(relative, normalized, false, false, entry.OriginalSize, entry.CompressedSize, entry.Modified, entry.Checksum, entry));
            }
        }

        var ordered = items.OrderByDescending(item => item.IsParent).ThenByDescending(item => item.IsDirectory).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        BrowserItems.ReplaceAll(ordered);
        OnPropertyChanged(nameof(VisibleFolderCount)); OnPropertyChanged(nameof(VisibleFileCount)); OnPropertyChanged(nameof(BrowserStatus)); OnPropertyChanged(nameof(BrowserItemCountText));
    }

    private bool MatchesSearch(string name) => string.IsNullOrWhiteSpace(SearchText) || name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    private async Task<T> WithArchivePassword<T>(string archivePath, Func<string?, Task<T>> action)
    {
        var fullPath = Path.GetFullPath(archivePath);
        _archivePasswords.TryGetValue(fullPath, out var password);
        while (true)
        {
            try
            {
                var result = await action(password);
                if (password is not null) _archivePasswords[fullPath] = password;
                return result;
            }
            catch (SharpCompress.Common.CryptographicException) when (RequestArchivePassword is not null)
            {
                _archivePasswords.Remove(fullPath);
                password = RequestArchivePassword(archivePath);
                if (password is null) throw new OperationCanceledException();
            }
        }
    }

    private Task WithArchivePassword(string archivePath, Func<string?, Task> action) =>
        WithArchivePassword(archivePath, async password => { await action(password); return true; });

    private async Task WithOperation(Func<CancellationToken, Task> operation)
    {
        if (IsBusy) return;
        using var cancellation = new CancellationTokenSource(); _cancellation = cancellation; IsBusy = true; ProgressPercent = 0;
        CurrentOperationFile = _localization["Preparing"]; OperationCounts = _localization["CheckingItems"]; ProcessedText = "0 B"; SpeedText = "0 B/s"; ElapsedText = "00:00"; RemainingText = _localization["Calculating"];
        try { await operation(cancellation.Token); }
        finally { IsBusy = false; _cancellation = null; }
    }
    private static string FormatBytes(double value) { string[] units = ["B", "KB", "MB", "GB"]; var i = 0; while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; } return $"{value:0.0} {units[i]}"; }
    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
    private void NotifyLocalized() { foreach (var name in new[] { nameof(Subtitle), nameof(NewArchiveText), nameof(ExtractText), nameof(OpenText), nameof(TestText), nameof(SettingsText), nameof(DropText), nameof(DropHint), nameof(CancelText), nameof(BrowserStatus), nameof(BrowserItemCountText) }) OnPropertyChanged(name); RebuildBrowserItems(); }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name);
        if (name == nameof(CurrentArchive)) { OnPropertyChanged(nameof(HasArchive)); OnPropertyChanged(nameof(CanModifyCurrentArchive)); OnPropertyChanged(nameof(BreadcrumbText)); }
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
