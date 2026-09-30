using System.ComponentModel;
using System.Windows;

namespace ZCompression.App;

public partial class OperationProgressWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public OperationProgressWindow(MainViewModel viewModel, string operationName, string targetName, bool isExtraction)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Title = $"{operationName} - z_compression";
        OperationTitle.Text = operationName;
        OperationTarget.Text = targetName;
        OperationIcon.Text = isExtraction ? "🗜️" : "📦";
    }

    public void Finish()
    {
        _allowClose = true;
        Close();
    }

    private void OnBackground(object sender, RoutedEventArgs e) => Hide();
    private void OnCancel(object sender, RoutedEventArgs e) => _viewModel.Cancel();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
}
