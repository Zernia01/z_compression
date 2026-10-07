using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using ZCompression.Core.Archives;

namespace ZCompression.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _sidebarCollapsed;
    private bool _detailsVisible = true;
    private bool _detailsAnimating;
    private Point? _archiveDragOrigin;
    private Point? _archiveTitleDragOrigin;
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

    private async void OnArchiveDragMove(object sender, MouseEventArgs e)
    {
        if (_archiveDragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed || _exportingDrag || _viewModel.IsBusy) return;
        var position = e.GetPosition(ArchiveGrid);
        if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _archiveDragOrigin = null;
        var items = ArchiveGrid.SelectedItems.Cast<ArchiveBrowserItem>().Where(item => !item.IsParent).ToArray();
        if (items.Length == 0) return;
        e.Handled = true;
        var selectedPaths = items.Select(item => item.Path).ToArray();
        var folder = _viewModel.CurrentFolder;
        await ExportArchiveSelectionAsync(() => ArchiveExportManifest.Create(_viewModel.Entries.ToArray(), selectedPaths, folder), selectedPaths, folder, false);
    }

    private void OnCommandBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ArchiveSearchBox is null) return;
        var compact = e.NewSize.Width < 720;
        SearchColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(ArchiveSearchBox, compact ? 1 : 0);
        Grid.SetColumn(ArchiveSearchBox, compact ? 0 : 2);
        Grid.SetColumnSpan(ArchiveSearchBox, compact ? 4 : 1);
        ArchiveSearchBox.MaxWidth = compact ? double.PositiveInfinity : 250;
        ArchiveSearchBox.Margin = compact ? new Thickness(0, 0, 0, 10) : new Thickness(12, 0, 12, 0);
    }

    private void OnArchiveTitleDragStart(object sender, MouseButtonEventArgs e)
    {
        _archiveTitleDragOrigin = !_viewModel.IsBusy && !_exportingDrag && _viewModel.HasArchive && e.ClickCount == 1
            ? e.GetPosition((IInputElement)sender) : null;
    }

    private async void OnArchiveTitleDragMove(object sender, MouseEventArgs e)
    {
        if (_archiveTitleDragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed || _exportingDrag || _viewModel.IsBusy) return;
        var position = e.GetPosition((IInputElement)sender);
        if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _archiveTitleDragOrigin = null;
        e.Handled = true;
        await ExportArchiveSelectionAsync(() => ArchiveExportManifest.CreateWholeArchive(_viewModel.Entries.ToArray(), _viewModel.CurrentArchivePath), null, "", true);
    }

    private async Task ExportArchiveSelectionAsync(Func<IReadOnlyList<ArchiveExportEntry>> createManifest, IReadOnlyList<string>? selectedPaths, string relativeRoot, bool wholeArchive)
    {
        _exportingDrag = true;
        OperationProgressWindow? progressWindow = null;
        try
        {
            await _viewModel.ExtractDroppedSelectionAsync(async token =>
            {
                var archive = _viewModel.CurrentArchivePath;
                var manifest = createManifest();
                var location = ArchiveDestinationDrag.Drag(manifest);
                if (location is null) return null;
                var destination = await ShellDropDestination.ResolveAsync(location, token);
                if (destination is null) throw new NotSupportedException(LocalizationManager.Instance["DropFolderUnsupported"]);
                var existing = await Task.Run(() => ArchiveDropExtraction.CountExistingFiles(destination, archive, manifest, token), token);
                if (existing > 0 && MessageBox.Show(this, string.Format(LocalizationManager.Instance["DropOverwriteConfirm"], existing), "z_compression", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return null;
                var password = _viewModel.GetDragExportPassword(manifest.Any(entry => entry.IsEncrypted));
                var request = ArchiveDropExtraction.CreateRequest(archive, destination, wholeArchive, selectedPaths, relativeRoot, password);
                progressWindow = new OperationProgressWindow(_viewModel, LocalizationManager.Instance["ExtractAction"], Path.GetFileName(archive), true) { Owner = this };
                progressWindow.Show();
                return request;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _viewModel.ForgetDragExportPassword();
            MessageBox.Show(this, _viewModel.FriendlyError(exception), "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { progressWindow?.Finish(); _exportingDrag = false; }
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
