using System.Windows;

namespace Kumparam;

public partial class CustomMessageBox : Window
{
    // Kullanıcının neye bastığını tutacak değişken
    public bool IsOk { get; private set; } = false;

    public CustomMessageBox(string title, string message, bool showCancelButton = false)
    {
        InitializeComponent();
        
        TitleText.Text = title;
        MessageText.Text = message;

        if (showCancelButton)
        {
            CancelButton.Visibility = Visibility.Visible;
            OkButton.Content = "EVET";
            CancelButton.Content = "HAYIR";
        }
    }

    // Statik Metot: Tek satırda çağırmak için
    public static bool Show(string title, string message, bool showCancelButton = false)
    {
        var msgBox = new CustomMessageBox(title, message, showCancelButton);
        msgBox.ShowDialog(); // Pencere kapanana kadar kodu durdurur
        return msgBox.IsOk;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        IsOk = true;
        this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsOk = false;
        this.Close();
    }
}