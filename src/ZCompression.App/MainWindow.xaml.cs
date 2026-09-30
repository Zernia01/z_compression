using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZCompression.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _sidebarCollapsed;
    private bool _detailsVisible = true;
    private bool _detailsAnimating;
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
        var destination = SelectFolder(LocalizationManager.Instance["SelectExtractionDestination"]);
        if (destination is not null) await RunWithProgressAsync(LocalizationManager.Instance["ExtractAction"], Path.GetFileName(archivePath), true, () => _viewModel.ExtractAsync(archivePath, destination));
    }

    private async void OnNewArchive(object sender, RoutedEventArgs e) => await CreateArchiveFromSourcesAsync();

    public async Task CreateArchiveFromSourcesAsync(IEnumerable<string>? initialSources = null)
    {
        var dialog = new NewArchiveWindow(initialSources) { Owner = this };
        if (dialog.ShowDialog() == true)
            await RunWithProgressAsync(LocalizationManager.Instance["CompressAction"], Path.GetFileName(dialog.DestinationPath), false,
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
            await CreateArchiveFromSourcesAsync(paths);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    private void OnCancel(object sender, RoutedEventArgs e) => _viewModel.Cancel();
    private void OnHome(object sender, RoutedEventArgs e) => _viewModel.ClearArchive();
    private void OnCloseDetails(object sender, RoutedEventArgs e) => SetDetailsVisible(false);
    private void OnToggleDetails(object sender, RoutedEventArgs e) => SetDetailsVisible(!_detailsVisible);

    private void SetDetailsVisible(bool visible)
    {
        if (_detailsAnimating || visible == _detailsVisible) return;
        _detailsAnimating = true;
        var transform = (TranslateTransform)DetailsPane.RenderTransform;
        var duration = TimeSpan.FromMilliseconds(190);
        if (visible)
        {
            DetailsColumn.Width = new GridLength(294);
            DetailsPane.Visibility = Visibility.Visible;
            DetailsPane.Opacity = 0;
            transform.X = 26;
            DetailsPane.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
            var slide = new DoubleAnimation(26, 0, duration);
            slide.Completed += (_, _) => { transform.X = 0; DetailsPane.Opacity = 1; _detailsVisible = true; _detailsAnimating = false; };
            transform.BeginAnimation(TranslateTransform.XProperty, slide);
        }
        else
        {
            DetailsPane.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, duration));
            var slide = new DoubleAnimation(0, 26, duration);
            slide.Completed += (_, _) =>
            {
                DetailsPane.Visibility = Visibility.Collapsed;
                DetailsColumn.Width = new GridLength(0);
                transform.X = 0;
                DetailsPane.Opacity = 1;
                _detailsVisible = false;
                _detailsAnimating = false;
            };
            transform.BeginAnimation(TranslateTransform.XProperty, slide);
        }
    }
    private void OnNavigateUp(object sender, RoutedEventArgs e) => _viewModel.NavigateUp();
    private async void OnArchiveItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = _viewModel.SelectedBrowserItem;
        if (item is null) return;
        if (item.IsDirectory) { _viewModel.OpenBrowserItem(item); return; }
        if (MainViewModel.IsPotentiallyExecutable(item.Name))
        {
            var result = MessageBox.Show(this,
                LocalizationManager.Instance["ExecutableWarning"],
                LocalizationManager.Instance["ExecutableWarningTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
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

    private static OpenFileDialog ArchiveOpenDialog() => new() { Filter = $"{LocalizationManager.Instance["Archives"]}|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.zst|{LocalizationManager.Instance["AllFiles"]}|*.*", CheckFileExists = true };
    private static string? SelectFolder(string title) { var dialog = new OpenFolderDialog { Title = title }; return dialog.ShowDialog() == true ? dialog.FolderName : null; }
}
