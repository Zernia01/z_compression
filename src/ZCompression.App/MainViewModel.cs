using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using ZCompression.Core.Archives;
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

    public MainViewModel(IArchiveEngine engine, ILocalizationService localization, ISettingsService settingsService, AppSettings settings)
    {
        _engine = engine; _localization = localization; _settingsService = settingsService; Settings = settings;
        FilteredEntries = CollectionViewSource.GetDefaultView(Entries);
        FilteredEntries.Filter = item => item is ArchiveEntryInfo entry && (string.IsNullOrWhiteSpace(SearchText) || entry.Path.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        _localization.LanguageChanged += (_, _) => NotifyLocalized();
        Status = _localization["Ready"];
    }

    public ObservableCollection<ArchiveEntryInfo> Entries { get; } = [];
    public ObservableCollection<ArchiveBrowserItem> BrowserItems { get; } = [];
    public ICollectionView FilteredEntries { get; }
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
    public string BrowserStatus => $"파일: {VisibleFileCount:N0}, 폴더: {VisibleFolderCount:N0}, 전체 항목: {Entries.Count:N0}";
    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value)) { FilteredEntries.Refresh(); RebuildBrowserItems(); } } }

    public async Task OpenArchiveAsync(string path) => await WithOperation(async token =>
    {
        Status = _localization["Opening"];
        var entries = await _engine.ListAsync(path, cancellationToken: token);
        Entries.Clear(); foreach (var entry in entries) Entries.Add(entry);
        CurrentArchivePath = Path.GetFullPath(path); CurrentArchive = Path.GetFileName(path); CurrentFolder = ""; SelectedEntry = null; SelectedBrowserItem = null; RebuildBrowserItems(); Status = string.Format(_localization["EntryCount"], entries.Count);
    });

    public async Task CompressAsync(IReadOnlyList<string> sources, string destination, ArchiveFormat format, CompressionPreset level = CompressionPreset.Normal) => await WithOperation(async token =>
    {
        Status = _localization["Compressing"];
        await _engine.CompressAsync(new CompressionRequest(sources, destination, format, level), CreateProgress(), token);
        var entries = await _engine.ListAsync(destination, cancellationToken: token);
        Entries.Clear(); foreach (var entry in entries) Entries.Add(entry);
        CurrentArchivePath = Path.GetFullPath(destination); CurrentArchive = Path.GetFileName(destination);
        CurrentFolder = ""; SelectedEntry = null; SelectedBrowserItem = null; RebuildBrowserItems();
        Status = _localization["CompressionComplete"];
    });

    public async Task ExtractAsync(string archive, string destination) => await WithOperation(async token =>
    {
        Status = _localization["Extracting"];
        await _engine.ExtractAsync(new ExtractionRequest(archive, destination), CreateProgress(), token);
        Status = _localization["ExtractionComplete"];
    });

    public async Task TestAsync(string archive) => await WithOperation(async token =>
    {
        Status = _localization["Testing"];
        await _engine.TestAsync(archive, cancellationToken: token);
        Status = _localization["ArchiveHealthy"];
    });

    public void Cancel() => _cancellation?.Cancel();
    public void ClearArchive()
    {
        Entries.Clear(); BrowserItems.Clear(); SelectedEntry = null; SelectedBrowserItem = null; CurrentFolder = ""; CurrentArchivePath = string.Empty; CurrentArchive = string.Empty; SearchText = string.Empty; Status = _localization["Ready"];
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
            Status = $"{item.Name} 여는 중…";
            await _engine.ExtractEntryAsync(CurrentArchivePath, item.Path, destination, cancellationToken: token);
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
    public Task SaveSettingsAsync() => _settingsService.SaveAsync(Settings);

    public string FriendlyError(Exception exception) => exception switch
    {
        InvalidDataException => _localization["InvalidArchive"],
        UnauthorizedAccessException => _localization["AccessDenied"],
        NotSupportedException => exception.Message,
        _ => _localization["UnexpectedError"],
    };

    public static bool IsArchivePath(string path) => new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static ArchiveFormat FormatFromPath(string path) => path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ? ArchiveFormat.TarGZip : Path.GetExtension(path).ToLowerInvariant() switch { ".7z" => ArchiveFormat.SevenZip, ".tar" => ArchiveFormat.Tar, ".gz" => ArchiveFormat.GZip, _ => ArchiveFormat.Zip };

    private Progress<ArchiveProgress> CreateProgress() => new(value =>
    {
        ProgressPercent = value.Percent;
        CurrentOperationFile = value.CurrentFile;
        OperationCounts = $"{value.CompletedFiles:N0} / {value.TotalFiles:N0}개 항목";
        ProcessedText = $"{FormatBytes(value.ProcessedBytes)} / {FormatBytes(value.TotalBytes)}";
        SpeedText = $"{FormatBytes(value.BytesPerSecond)}/s";
        ElapsedText = FormatDuration(value.Elapsed);
        var remaining = value.Percent > 0 && value.Percent < 100
            ? TimeSpan.FromTicks((long)(value.Elapsed.Ticks * (100d - value.Percent) / value.Percent)) : TimeSpan.Zero;
        RemainingText = remaining > TimeSpan.Zero ? FormatDuration(remaining) : "계산 중";
        ProgressDetails = $"{OperationCounts} · {SpeedText} · {value.CurrentFile}";
    });
    private void RebuildBrowserItems()
    {
        BrowserItems.Clear();
        if (!string.IsNullOrEmpty(CurrentFolder)) BrowserItems.Add(new ArchiveBrowserItem("..", CurrentFolder, true, true, 0, 0, null, null, null));

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
                if (MatchesSearch(folder) && folders.Add(folder)) BrowserItems.Add(new ArchiveBrowserItem(folder, prefix + folder, true, false, 0, 0, entry.Modified, null, null));
                continue;
            }
            if (entry.IsDirectory)
            {
                if (MatchesSearch(relative) && folders.Add(relative)) BrowserItems.Add(new ArchiveBrowserItem(relative, normalized, true, false, 0, 0, entry.Modified, entry.Checksum, entry));
            }
            else if (MatchesSearch(relative))
            {
                BrowserItems.Add(new ArchiveBrowserItem(relative, normalized, false, false, entry.OriginalSize, entry.CompressedSize, entry.Modified, entry.Checksum, entry));
            }
        }

        var ordered = BrowserItems.OrderByDescending(item => item.IsParent).ThenByDescending(item => item.IsDirectory).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        BrowserItems.Clear(); foreach (var item in ordered) BrowserItems.Add(item);
        OnPropertyChanged(nameof(VisibleFolderCount)); OnPropertyChanged(nameof(VisibleFileCount)); OnPropertyChanged(nameof(BrowserStatus));
    }

    private bool MatchesSearch(string name) => string.IsNullOrWhiteSpace(SearchText) || name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    private async Task WithOperation(Func<CancellationToken, Task> operation)
    {
        if (IsBusy) return;
        using var cancellation = new CancellationTokenSource(); _cancellation = cancellation; IsBusy = true; ProgressPercent = 0;
        CurrentOperationFile = "준비 중…"; OperationCounts = "항목 확인 중"; ProcessedText = "0 B"; SpeedText = "0 B/s"; ElapsedText = "00:00"; RemainingText = "계산 중";
        try { await operation(cancellation.Token); }
        finally { IsBusy = false; _cancellation = null; }
    }
    private static string FormatBytes(double value) { string[] units = ["B", "KB", "MB", "GB"]; var i = 0; while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; } return $"{value:0.0} {units[i]}"; }
    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
    private void NotifyLocalized() { foreach (var name in new[] { nameof(Subtitle), nameof(NewArchiveText), nameof(ExtractText), nameof(OpenText), nameof(TestText), nameof(SettingsText), nameof(DropText), nameof(DropHint), nameof(CancelText) }) OnPropertyChanged(name); }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name);
        if (name == nameof(CurrentArchive)) { OnPropertyChanged(nameof(HasArchive)); OnPropertyChanged(nameof(BreadcrumbText)); }
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
