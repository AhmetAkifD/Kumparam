using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Core.Models;
// YENİ NAMESPACE'İ BURAYA EKLEDİK
using Kumparam.Pages.DashboardSubPages; 

namespace Kumparam.Pages;

public partial class DashboardWindow : Window
{
    private readonly User _currentUser;

    public DashboardWindow(User user)
    {
        InitializeComponent();
        _currentUser = user;
        WelcomeText.Text = _currentUser.Email;

        // Varsayılan sayfa: Özet Durum
        MainContentArea.Content = new SummaryView(_currentUser.UserId);
        CheckScreenResolution();
        if (_currentUser.IsAdmin)
        {
            AdminPanelButton.Visibility = Visibility.Visible;
        }
    }
    
    private void Logout_Button_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new Pages.MainWindow();
        loginWindow.Show();
        this.Close();
    }
    private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new AdminView(); // Admin sayfasını aç
    }

    // Yeni: Metotları güncelledik, artık ID gönderiyoruz
    private void SummaryButton_Click(object sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new SummaryView(_currentUser.UserId);
    }

    private void IncomeExpenseButton_Click(object sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new IncomeExpenseView(_currentUser.UserId);
    }

    private void InvestmentsButton_Click(object sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new InvestmentsView(_currentUser.UserId);
    }

    private void GoalsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hata muhtemelen bu satır çalışırken (Constructor veya LoadGoals içinde) oluyor
            MainContentArea.Content = new GoalsView(_currentUser.UserId);
        }
        catch (Exception ex)
        {
            // Uygulamayı kapatma, hatayı yüzümüze söyle!
            MessageBox.Show($"HATA DETAYI:\n\n{ex.Message}\n\nKAYNAK:\n{ex.StackTrace}", 
                "Hedefler Sayfası Açılmadı", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }
    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        MainContentArea.Content = new ProfileView(_currentUser.UserId, _currentUser.Email);
    }
    private void CheckScreenResolution()
    {
        // Ekran genişliğini ve yüksekliğini al
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        // Hedeflenen minimum boyutlar
        double targetMinW = 1280;
        double targetMinH = 720;

        // Eğer ekran, hedefimizden küçükse (Laptop vs.)
        if (screenWidth < targetMinW || screenHeight < targetMinH)
        {
            // Özel mesaj kutumuzla haber verelim (Ses yok, şık tasarım var)
            CustomMessageBox.Show(
                "Ekran Çözünürlüğü Uyarısı", 
                $"Ekran çözünürlüğünüz ({screenWidth}x{screenHeight}), önerilen boyutlardan (1280x720) düşük. " +
                "Uygulama kısıtlama olmadan açılacak ancak bazı görüntüler taşabilir."
            );
        
            // Kısıtlama koymuyoruz (veya ekran boyutuna eşitliyoruz)
            this.MinWidth = 800; // Daha güvenli, çok küçük bir alt sınır
            this.MinHeight = 600;
        
            // İstersen pencereyi ekranı kaplatabilirsin
            this.WindowState = WindowState.Maximized;
        }
        else
        {
            // Ekran büyükse kısıtlamayı koy
            this.MinWidth = targetMinW;
            this.MinHeight = targetMinH;
        
            // İstersen başlangıç boyutu olarak da ata
            this.Width = targetMinW;
            this.Height = targetMinH;
        }
    }
}