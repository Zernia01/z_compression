using System.IO;
using System.Windows;

namespace ZCompression.App;

public partial class ArchivePasswordWindow : Window
{
    public ArchivePasswordWindow(string archivePath)
    {
        InitializeComponent();
        ArchiveNameText.Text = Path.GetFileName(archivePath);
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string Password => PasswordInput.Password;
    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (Password.Length > 0) DialogResult = true;
    }
}
