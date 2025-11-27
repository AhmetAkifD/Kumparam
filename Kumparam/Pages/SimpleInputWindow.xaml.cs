using System.Windows;

namespace Kumparam;

public partial class SimpleInputWindow : Window
{
    public string ResultText { get; private set; } = string.Empty;

    public SimpleInputWindow(string title = "Giriş")
    {
        InitializeComponent();
        // İstersen başlığı da değiştirebilirsin
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputTextBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}