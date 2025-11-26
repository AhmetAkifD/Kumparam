using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
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
    }
    
    private void Logout_Button_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new Pages.MainWindow();
        loginWindow.Show();
        this.Close();
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
        MainContentArea.Content = new InvestmentsView();
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
}