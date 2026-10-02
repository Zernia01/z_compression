using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ZCompression.Core.Archives;

namespace ZCompression.App;

public partial class NewArchiveWindow : Window, INotifyPropertyChanged
{
    public NewArchiveWindow(IEnumerable<string>? initialSources = null)
    {
        InitializeComponent();
        DataContext = this;
        LocationBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        FileNameBox.Text = LocalizationManager.Instance["DefaultArchiveName"];
        Sources.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceCountText)));
        if (initialSources is not null) AddSources(initialSources);
    }

    public ObservableCollection<CompressionSourceItem> Sources { get; } = [];
    public string SourceCountText => string.Format(LocalizationManager.Instance["ItemCount"], Sources.Count);
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> SelectedSources => Sources.Select(item => item.FullPath).ToArray();
    public string DestinationPath { get; private set; } = "";
    public ArchiveFormat SelectedFormat { get; private set; } = ArchiveFormat.Zip;
    public CompressionPreset SelectedLevel { get; private set; } = CompressionPreset.High;
    public string? SelectedPassword => SelectedFormat == ArchiveFormat.Rar && PasswordBox.Password.Length > 0 ? PasswordBox.Password : null;

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = LocalizationManager.Instance["AddCompressionFilesTitle"], Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) AddSources(dialog.FileNames);
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = LocalizationManager.Instance["AddCompressionFolderTitle"], Multiselect = true };
        if (dialog.ShowDialog(this) == true) AddSources(dialog.FolderNames);
    }

    private void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        foreach (var item in SourceList.SelectedItems.Cast<CompressionSourceItem>().ToArray()) Sources.Remove(item);
    }

    private void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = LocalizationManager.Instance["SaveArchiveLocationTitle"] };
        if (Directory.Exists(LocationBox.Text)) dialog.InitialDirectory = LocationBox.Text;
        if (dialog.ShowDialog(this) == true) LocationBox.Text = dialog.FolderName;
    }

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileNameBox is null || FormatBox.SelectedItem is not ComboBoxItem { Tag: string format }) return;
        var extension = format switch { "Rar" => ".rar", "SevenZip" => ".7z", "Tar" => ".tar", "TarGZip" => ".tar.gz", _ => ".zip" };
        FileNameBox.Text = StripArchiveExtension(FileNameBox.Text) + extension;
        if (RarOptionsPanel is not null) RarOptionsPanel.Visibility = format == "Rar" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnStartCompression(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (Sources.Count == 0) { ValidationText.Text = LocalizationManager.Instance["ValidationSources"]; return; }
        if (string.IsNullOrWhiteSpace(LocationBox.Text)) { ValidationText.Text = LocalizationManager.Instance["ValidationLocation"]; return; }
        if (string.IsNullOrWhiteSpace(FileNameBox.Text)) { ValidationText.Text = LocalizationManager.Instance["ValidationName"]; return; }
        if (FileNameBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { ValidationText.Text = LocalizationManager.Instance["ValidationInvalidName"]; return; }

        SelectedFormat = ParseEnumTag(FormatBox, ArchiveFormat.Zip);
        if (SelectedFormat == ArchiveFormat.Rar && RarCommandLine.FindExecutable() is null)
        {
            ValidationText.Text = LocalizationManager.Instance["RarToolRequired"];
            return;
        }
        SelectedLevel = ParseEnumTag(LevelBox, CompressionPreset.High);
        var extension = SelectedFormat switch { ArchiveFormat.Rar => ".rar", ArchiveFormat.SevenZip => ".7z", ArchiveFormat.Tar => ".tar", ArchiveFormat.TarGZip => ".tar.gz", _ => ".zip" };
        var fileName = StripArchiveExtension(FileNameBox.Text) + extension;
        DestinationPath = Path.GetFullPath(Path.Combine(LocationBox.Text.Trim(), fileName));
        DialogResult = true;
    }

    private void AddSources(IEnumerable<string> paths)
    {
        foreach (var path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Sources.Any(item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))) continue;
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path)) continue;
            var size = isDirectory ? 0 : new FileInfo(path).Length;
            Sources.Add(new CompressionSourceItem(path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), isDirectory, size));
        }
        if (Sources.Count == 1 && FileNameBox.Text == LocalizationManager.Instance["DefaultArchiveName"]) FileNameBox.Text = StripArchiveExtension(Sources[0].Name) + CurrentExtension();
    }

    private string CurrentExtension() => FormatBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag switch { "Rar" => ".rar", "SevenZip" => ".7z", "Tar" => ".tar", "TarGZip" => ".tar.gz", _ => ".zip" } : ".zip";
    private static string StripArchiveExtension(string name)
    {
        foreach (var extension in new[] { ".tar.gz", ".zip", ".rar", ".7z", ".tar", ".tgz" })
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return name[..^extension.Length];
        return name;
    }
    private static T ParseEnumTag<T>(ComboBox comboBox, T fallback) where T : struct =>
        comboBox.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse<T>(tag, out var value) ? value : fallback;
}
