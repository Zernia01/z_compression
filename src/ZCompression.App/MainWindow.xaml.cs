using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;

namespace ZCompression.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _sidebarCollapsed;
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = _viewModel = viewModel; }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        var dialog = ArchiveOpenDialog();
        if (dialog.ShowDialog(this) == true) await RunUiAction(() => _viewModel.OpenArchiveAsync(dialog.FileName));
    }

    private async void OnExtract(object sender, RoutedEventArgs e)
    {
        var archivePath = _viewModel.CurrentArchivePath;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            var archive = ArchiveOpenDialog();
            if (archive.ShowDialog(this) != true) return;
            archivePath = archive.FileName;
        }
        var destination = SelectFolder("Select extraction destination");
        if (destination is not null) await RunWithProgressAsync("압축 풀기", Path.GetFileName(archivePath), true, () => _viewModel.ExtractAsync(archivePath, destination));
    }

    private async void OnNewArchive(object sender, RoutedEventArgs e)
    {
        var dialog = new NewArchiveWindow { Owner = this };
        if (dialog.ShowDialog() == true)
            await RunWithProgressAsync("압축하기", Path.GetFileName(dialog.DestinationPath), false,
                () => _viewModel.CompressAsync(dialog.SelectedSources, dialog.DestinationPath, dialog.SelectedFormat, dialog.SelectedLevel));
    }

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        var archivePath = _viewModel.CurrentArchivePath;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            var dialog = ArchiveOpenDialog();
            if (dialog.ShowDialog(this) != true) return;
            archivePath = dialog.FileName;
        }
        await RunUiAction(() => _viewModel.TestAsync(archivePath));
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (paths.Length == 1 && MainViewModel.IsArchivePath(paths[0])) await RunUiAction(() => _viewModel.OpenArchiveAsync(paths[0]));
        else
        {
            var dialog = new NewArchiveWindow(paths) { Owner = this };
            if (dialog.ShowDialog() == true)
                await RunWithProgressAsync("압축하기", Path.GetFileName(dialog.DestinationPath), false,
                    () => _viewModel.CompressAsync(dialog.SelectedSources, dialog.DestinationPath, dialog.SelectedFormat, dialog.SelectedLevel));
        }
    }

    private void OnDragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    private void OnCancel(object sender, RoutedEventArgs e) => _viewModel.Cancel();
    private void OnHome(object sender, RoutedEventArgs e) => _viewModel.ClearArchive();
    private void OnCloseDetails(object sender, RoutedEventArgs e) => _viewModel.SelectedEntry = null;
    private void OnNavigateUp(object sender, RoutedEventArgs e) => _viewModel.NavigateUp();
    private async void OnArchiveItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = _viewModel.SelectedBrowserItem;
        if (item is null) return;
        if (item.IsDirectory) { _viewModel.OpenBrowserItem(item); return; }
        if (MainViewModel.IsPotentiallyExecutable(item.Name))
        {
            var result = MessageBox.Show(this,
                "압축 파일 안의 실행 가능한 파일은 컴퓨터에 위험할 수 있습니다. 신뢰할 수 있는 파일인 경우에만 여세요.\n\n계속 여시겠습니까?",
                "실행 파일 열기 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
        }
        await RunUiAction(async () =>
        {
            var path = await _viewModel.ExtractForOpenAsync(item);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }

    private void OnSidebarToggle(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 62 : 218);
        ExpandedSidebar.Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedSidebar.Visibility = _sidebarCollapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnSettings(object sender, RoutedEventArgs e)
    {
        var previousEffect = Effect;
        var previousOpacity = Opacity;
        Effect = new BlurEffect { Radius = 7 };
        Opacity = 0.72;
        try { new SettingsWindow(_viewModel) { Owner = this }.ShowDialog(); }
        finally { Effect = previousEffect; Opacity = previousOpacity; }
        await _viewModel.SaveSettingsAsync();
    }

    private async Task RunUiAction(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { MessageBox.Show(this, _viewModel.FriendlyError(exception), "z_compression", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task RunWithProgressAsync(string operationName, string targetName, bool isExtraction, Func<Task> action)
    {
        var progressWindow = new OperationProgressWindow(_viewModel, operationName, targetName, isExtraction) { Owner = this };
        progressWindow.Show();
        try { await RunUiAction(action); }
        finally { progressWindow.Finish(); }
    }

    private static OpenFileDialog ArchiveOpenDialog() => new() { Filter = "Archives|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.zst|All files|*.*", CheckFileExists = true };
    private static string? SelectFolder(string title) { var dialog = new OpenFolderDialog { Title = title }; return dialog.ShowDialog() == true ? dialog.FolderName : null; }
}
