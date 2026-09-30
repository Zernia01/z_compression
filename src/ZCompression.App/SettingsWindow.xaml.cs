using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZCompression.App;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        SelectTag(LanguageBox, viewModel.Settings.Language);
        SelectTheme(viewModel.Settings.Theme);
    }

    private void OnCategoryChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string category }) return;
        GeneralPanel.Visibility = category == "general" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = category == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        PerformancePanel.Visibility = category == "performance" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = category == "about" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { GeneralNav, AppearanceNav, PerformanceNav, AboutNav }) button.Tag = null;
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

    private void OnDonate(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://buymeacoffee.com/zernia") { UseShellExecute = true });

    private void OnSetDefaultApps(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException(LocalizationManager.Instance["UnexpectedError"]);
            FileAssociationService.RegisterCurrentUser(executable);
            FileAssociationService.OpenDefaultAppsSettings();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"{LocalizationManager.Instance["DefaultAppsError"]}\n{exception.Message}", "z_compression", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
