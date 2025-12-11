using System.Windows;

namespace Kumparam;

public partial class SimpleInputWindow : Window
{
    public string ResultText { get; private set; } = string.Empty;

    public SimpleInputWindow(string message, string defaultInput = "")
    {
        InitializeComponent();
        
        // Gelen mesajı ekrana yazıyoruz (XAML'de isim verdiğimiz için artık erişebiliyoruz)
        MessageTextBlock.Text = message;

        // Varsa varsayılan değeri kutuya koy (örn: düzenleme yaparken eski değer gelsin)
        if (!string.IsNullOrEmpty(defaultInput))
        {
            InputTextBox.Text = defaultInput;
            InputTextBox.SelectAll(); // Kolay silinsin diye seçili gelsin
        }

        InputTextBox.Focus(); // İmleci kutuya odakla
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputTextBox.Text;
        DialogResult = true;
        Close(); // Pencereyi kapat
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close(); // Pencereyi kapat
    }
}