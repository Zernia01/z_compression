using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Threading;
using ZCompression.Core.Security;

namespace ZCompression.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _sidebarCollapsed;
    private bool _detailsVisible = true;
    private bool _detailsAnimating;
    private Point? _archiveDragOrigin;
    private bool _exportingDrag;
    private const string ArchiveDragFormat = "ZCompression.ArchiveSelection";
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.RequestArchivePassword = path =>
        {
            var dialog = new ArchivePasswordWindow(path) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Password : null;
        };
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnShortcutKeyDown), true);
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        var dialog = ArchiveOpenDialog();
        if (dialog.ShowDialog(this) == true) await RunUiAction(() => _viewModel.OpenArchiveAsync(dialog.FileName));
    }

    private async void OnExtract(object sender, RoutedEventArgs e) => await ExtractArchiveAsync();

    private async Task ExtractArchiveAsync()
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
                () => _viewModel.CompressAsync(dialog.SelectedSources, dialog.DestinationPath, dialog.SelectedFormat, dialog.SelectedLevel, dialog.SelectedPassword));
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
        if (e.Data.GetDataPresent(ArchiveDragFormat)) { e.Handled = true; e.Effects = DragDropEffects.None; return; }
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (_viewModel.HasArchive) await AddToOpenArchiveAsync(paths);
        else if (paths.Length == 1 && MainViewModel.IsArchivePath(paths[0])) await RunUiAction(() => _viewModel.OpenArchiveAsync(paths[0]));
        else
        {
            await CreateArchiveFromSourcesAsync(paths);
        }
    }

    private async void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Instance["AddCompressionFilesTitle"],
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true) await AddToOpenArchiveAsync(dialog.FileNames);
    }

    private async Task AddToOpenArchiveAsync(IReadOnlyList<string> paths)
    {
        if (!_viewModel.HasArchive || paths.Count == 0) return;
        await RunWithProgressAsync(LocalizationManager.Instance["AddFiles"], Path.GetFileName(_viewModel.CurrentArchivePath), false,
            () => _viewModel.AddToCurrentArchiveAsync(paths));
    }

    private async void OnShortcutKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.IsBusy || Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (ShortcutGesture.Matches(_viewModel.Settings.CompressShortcut, key, Keyboard.Modifiers))
        {
            e.Handled = true;
            await CreateArchiveFromSourcesAsync();
        }
        else if (ShortcutGesture.Matches(_viewModel.Settings.ExtractShortcut, key, Keyboard.Modifiers))
        {
            e.Handled = true;
            await ExtractArchiveAsync();
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !e.Data.GetDataPresent(ArchiveDragFormat) && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnArchiveDragStart(object sender, MouseButtonEventArgs e)
    {
        _archiveDragOrigin = null;
        if (_viewModel.IsBusy || _exportingDrag || e.ClickCount != 1) return;
        var row = FindArchiveRow(e.OriginalSource as DependencyObject);
        if (row?.Item is not ArchiveBrowserItem { IsParent: false }) return;
        _archiveDragOrigin = e.GetPosition(ArchiveGrid);
        // Keep a multi-selection when dragging a row already selected.
        if (row.IsSelected && ArchiveGrid.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None)
            e.Handled = true;
    }

    private void OnArchiveDragMove(object sender, MouseEventArgs e)
    {
        if (_archiveDragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed || _exportingDrag || _viewModel.IsBusy) return;
        var position = e.GetPosition(ArchiveGrid);
        if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _archiveDragOrigin = null;
        var items = ArchiveGrid.SelectedItems.Cast<ArchiveBrowserItem>().Where(item => !item.IsParent).ToArray();
        if (items.Length == 0) return;
        e.Handled = true;
        _exportingDrag = true;
        var exportRoot = Path.Combine(Path.GetTempPath(), "z_compression", "drag");
        var staging = SafeExtractionPath.Resolve(exportRoot, Guid.NewGuid().ToString("N"));
        var succeeded = false;
        try
        {
            // FileDrop lets Explorer choose the destination and handle name conflicts.
            var paths = items.Select(item => SafeExtractionPath.Resolve(staging, item.Name)).ToArray();
            var data = new DataObject(DataFormats.FileDrop, paths);
            data.SetData(ArchiveDragFormat, true);
            var deferred = new DeferredArchiveDataObject((System.Runtime.InteropServices.ComTypes.IDataObject)data,
                (short)DataFormats.GetDataFormat(DataFormats.FileDrop).Id, () => Dispatcher.Invoke(() =>
                {
                    var progress = new OperationProgressWindow(_viewModel, LocalizationManager.Instance["ExtractAction"], string.Join(", ", items.Select(item => item.Name)), true) { Owner = this, ShowActivated = false };
                    progress.Show();
                    try
                    {
                        var task = _viewModel.PrepareDragExportAsync(items, staging);
                        // Keep password prompts, progress, and cancellation responsive inside OLE GetData.
                        var frame = new DispatcherFrame();
                        _ = task.ContinueWith(_ => Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
                        Dispatcher.PushFrame(frame);
                        task.GetAwaiter().GetResult();
                        if (paths.Any(path => !File.Exists(path) && !Directory.Exists(path)))
                            throw new FileNotFoundException("A selected archive item could not be extracted.");
                    }
                    finally { progress.Finish(); }
                }));
            succeeded = DragDrop.DoDragDrop(ArchiveGrid, deferred, DragDropEffects.Copy) != DragDropEffects.None;
            if (deferred.Error is not null) throw deferred.Error;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { MessageBox.Show(this, _viewModel.FriendlyError(exception), "z_compression", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally
        {
            _exportingDrag = false;
            // Some drop targets copy asynchronously; retain successful exports for startup cleanup.
            if (!succeeded)
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
    }

    private static DataGridRow? FindArchiveRow(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is DataGridRow row) return row;
            element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }
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
        try
        {
            if (Environment.ProcessPath is { } executable) FileAssociationService.RegisterContextMenus(executable, _viewModel.Settings);
        }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
        catch (IOException) { }
        App.ApplyProcessPriority(_viewModel.Settings.OperationPriority);
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
