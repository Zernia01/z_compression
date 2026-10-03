using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZCompression.Core.Settings;

namespace ZCompression.App;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosing;
    private Button? _shortcutCaptureButton;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnShortcutKeyDown), true);
        SelectTag(LanguageBox, viewModel.Settings.Language);
        SelectTheme(viewModel.Settings.Theme);
        SelectTag(CpuThreadsBox, viewModel.Settings.CpuThreads.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SelectTag(PriorityBox, viewModel.Settings.OperationPriority);
        AutomaticUpdatesBox.IsChecked = viewModel.Settings.CheckForUpdatesAtStartup;
        RefreshShortcutButtons();
    }

    private void OnCategoryChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string category }) return;
        GeneralPanel.Visibility = category == "general" ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsPanel.Visibility = category == "shortcuts" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = category == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        PerformancePanel.Visibility = category == "performance" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = category == "about" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { GeneralNav, ShortcutsNav, AppearanceNav, PerformanceNav, AboutNav }) button.Tag = null;
        ((Button)sender).Tag = "Selected";
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is ComboBoxItem { Tag: string tag }) _viewModel.SetLanguage(tag);
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag })
        {
            _viewModel.SetTheme(tag);
            ThemeManager.Apply(tag);
        }
    }

    private void OnCpuThreadsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CpuThreadsBox.SelectedItem is ComboBoxItem { Tag: string value } && int.TryParse(value, out var threads))
            _viewModel.SetCpuThreads(threads);
    }

    private void OnPriorityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PriorityBox.SelectedItem is ComboBoxItem { Tag: string priority })
            _viewModel.SetOperationPriority(priority);
    }

    private void OnAutomaticUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (AutomaticUpdatesBox is not null)
            _viewModel.SetCheckForUpdatesAtStartup(AutomaticUpdatesBox.IsChecked == true);
    }

    private void OnShortcutCaptureClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        _shortcutCaptureButton = button;
        ShortcutValidationText.Text = string.Empty;
        button.Content = LocalizationManager.Instance["PressShortcut"];
        Keyboard.Focus(button);
    }

    private async void OnShortcutKeyDown(object sender, KeyEventArgs e)
    {
        if (_shortcutCaptureButton is not { } button) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (ShortcutGesture.IsModifierKey(key)) return;
        if (key == Key.Escape) { FinishShortcutCapture(); return; }

        var shortcut = string.Empty;
        if (key is not (Key.Delete or Key.Back) && !ShortcutGesture.TryCreate(key, Keyboard.Modifiers, out shortcut))
        {
            ShortcutValidationText.Text = LocalizationManager.Instance["ShortcutInvalidV2"];
            return;
        }

        var otherShortcut = Equals(button.Tag, "compress") ? _viewModel.Settings.ExtractShortcut : _viewModel.Settings.CompressShortcut;
        var menuKey = ExplorerMenuShortcut.GetAccessKey(shortcut);
        if (!string.IsNullOrEmpty(shortcut) && (shortcut.Equals(otherShortcut, StringComparison.OrdinalIgnoreCase) ||
            (menuKey is not null && menuKey == ExplorerMenuShortcut.GetAccessKey(otherShortcut))))
        {
            ShortcutValidationText.Text = LocalizationManager.Instance["ShortcutConflict"];
            return;
        }

        var compress = Equals(button.Tag, "compress") ? shortcut : _viewModel.Settings.CompressShortcut;
        var extract = Equals(button.Tag, "extract") ? shortcut : _viewModel.Settings.ExtractShortcut;
        _viewModel.SetShortcuts(compress, extract);
        ShortcutValidationText.Text = string.Empty;
        FinishShortcutCapture();
        try
        {
            await _viewModel.SaveSettingsAsync();
            if (Environment.ProcessPath is { } executable) FileAssociationService.RegisterContextMenus(executable, _viewModel.Settings);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShortcutValidationText.Text = LocalizationManager.Instance["ShortcutSyncFailed"];
        }
    }

    private void FinishShortcutCapture()
    {
        _shortcutCaptureButton = null;
        RefreshShortcutButtons();
    }

    private void RefreshShortcutButtons()
    {
        CompressShortcutButton.Content = DisplayShortcut(_viewModel.Settings.CompressShortcut);
        ExtractShortcutButton.Content = DisplayShortcut(_viewModel.Settings.ExtractShortcut);
    }

    private static string DisplayShortcut(string shortcut) => string.IsNullOrWhiteSpace(shortcut) ? LocalizationManager.Instance["ShortcutNone"] : shortcut;

    private async void OnCheckForUpdates(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        try { await UpdateCoordinator.CheckAndInstallAsync(this, showUpToDateMessage: true); }
        finally { if (sender is Button completedButton) completedButton.IsEnabled = true; }
    }

    private void SelectTheme(string theme)
    {
        var selected = theme switch { "light" => LightTheme, "dark" => DarkTheme, _ => SystemTheme };
        selected.IsChecked = true;
    }

    private static void SelectTag(ComboBox comboBox, string tag) =>
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, tag)) ?? comboBox.Items[0];

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(this).Y < 58) DragMove();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        var scale = (ScaleTransform)SettingsCard.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(180)));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void OnDonate(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://buymeacoffee.com/zernia") { UseShellExecute = true });

    private void OnSetDefaultApps(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException(LocalizationManager.Instance["UnexpectedError"]);
            FileAssociationService.RegisterCurrentUser(executable, _viewModel.Settings);
            FileAssociationService.OpenDefaultAppsSettings();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"{LocalizationManager.Instance["DefaultAppsError"]}\n{exception.Message}", "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
        var scale = (ScaleTransform)SettingsCard.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.97, TimeSpan.FromMilliseconds(150)));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.97, TimeSpan.FromMilliseconds(150)));
    }
}
