using System.Windows;
using System.Windows.Controls;

namespace ZCompression.App;

internal sealed class UpdateProgressWindow : Window
{
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Height = 8, IsIndeterminate = true, Maximum = 100 };
    internal CancellationTokenSource Cancellation { get; } = new();

    internal UpdateProgressWindow(Window owner)
    {
        Owner = owner;
        Title = LocalizationManager.Instance["AutomaticUpdates"];
        Width = 430; Height = 185; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(_status); panel.Children.Add(_progress);
        var cancel = new Button { Content = LocalizationManager.Instance["Cancel"], HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0), MinWidth = 75 };
        cancel.Click += (_, _) => Cancellation.Cancel();
        panel.Children.Add(cancel); Content = panel;
        Closed += (_, _) => Cancellation.Cancel();
        SetStage("CheckingUpdate");
    }

    internal void SetStage(string key) { _status.Text = LocalizationManager.Instance[key]; _progress.IsIndeterminate = true; }
    internal void Report(double percent)
    {
        _status.Text = $"{LocalizationManager.Instance["DownloadingUpdate"]} · {percent:0}%";
        _progress.IsIndeterminate = false; _progress.Value = percent;
    }
}
