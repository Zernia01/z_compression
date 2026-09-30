using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ZCompression.Core.Archives;

namespace ZCompression.App;

public partial class NewArchiveWindow : Window
{
    public NewArchiveWindow(IEnumerable<string>? initialSources = null)
    {
        InitializeComponent();
        DataContext = this;
        LocationBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        FileNameBox.Text = "새 압축.zip";
        if (initialSources is not null) AddSources(initialSources);
    }

    public ObservableCollection<CompressionSourceItem> Sources { get; } = [];
    public IReadOnlyList<string> SelectedSources => Sources.Select(item => item.FullPath).ToArray();
    public string DestinationPath { get; private set; } = "";
    public ArchiveFormat SelectedFormat { get; private set; } = ArchiveFormat.Zip;
    public CompressionPreset SelectedLevel { get; private set; } = CompressionPreset.Normal;

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "압축할 파일 추가", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) AddSources(dialog.FileNames);
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "압축할 폴더 추가", Multiselect = true };
        if (dialog.ShowDialog(this) == true) AddSources(dialog.FolderNames);
    }

    private void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        foreach (var item in SourceList.SelectedItems.Cast<CompressionSourceItem>().ToArray()) Sources.Remove(item);
    }

    private void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "압축 파일 저장 위치" };
        if (Directory.Exists(LocationBox.Text)) dialog.InitialDirectory = LocationBox.Text;
        if (dialog.ShowDialog(this) == true) LocationBox.Text = dialog.FolderName;
    }

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileNameBox is null || FormatBox.SelectedItem is not ComboBoxItem { Tag: string format }) return;
        var extension = format switch { "SevenZip" => ".7z", "Tar" => ".tar", "TarGZip" => ".tar.gz", _ => ".zip" };
        FileNameBox.Text = StripArchiveExtension(FileNameBox.Text) + extension;
    }

    private void OnStartCompression(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (Sources.Count == 0) { ValidationText.Text = "압축할 파일이나 폴더를 하나 이상 추가하세요."; return; }
        if (string.IsNullOrWhiteSpace(LocationBox.Text)) { ValidationText.Text = "저장 위치를 선택하세요."; return; }
        if (string.IsNullOrWhiteSpace(FileNameBox.Text)) { ValidationText.Text = "압축 파일 이름을 입력하세요."; return; }
        if (FileNameBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { ValidationText.Text = "파일 이름에 사용할 수 없는 문자가 있습니다."; return; }

        SelectedFormat = ParseEnumTag(FormatBox, ArchiveFormat.Zip);
        SelectedLevel = ParseEnumTag(LevelBox, CompressionPreset.Normal);
        var extension = SelectedFormat switch { ArchiveFormat.SevenZip => ".7z", ArchiveFormat.Tar => ".tar", ArchiveFormat.TarGZip => ".tar.gz", _ => ".zip" };
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
        if (Sources.Count == 1 && FileNameBox.Text == "새 압축.zip") FileNameBox.Text = StripArchiveExtension(Sources[0].Name) + CurrentExtension();
    }

    private string CurrentExtension() => FormatBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag switch { "SevenZip" => ".7z", "Tar" => ".tar", "TarGZip" => ".tar.gz", _ => ".zip" } : ".zip";
    private static string StripArchiveExtension(string name)
    {
        foreach (var extension in new[] { ".tar.gz", ".zip", ".7z", ".tar", ".tgz" })
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return name[..^extension.Length];
        return name;
    }
    private static T ParseEnumTag<T>(ComboBox comboBox, T fallback) where T : struct =>
        comboBox.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse<T>(tag, out var value) ? value : fallback;
}
