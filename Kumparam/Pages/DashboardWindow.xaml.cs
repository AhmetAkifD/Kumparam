using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using Kumparam.Core;
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using Kumparam.Pages.DashboardSubPages; 
using MaterialDesignThemes.Wpf;


namespace Kumparam.Pages;

public partial class DashboardWindow : Window
{
    private readonly User _currentUser;
    public static event Action ThemeChanged;
    private readonly IUserRepository _userRepository;

    public DashboardWindow(User user)
    {
        InitializeComponent();
        _currentUser = user;
        
        // 1. Veritabanı bağlantısını hazırla
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        IUserRepository userRepository = new SqlUserRepository(connectionString);
        _userRepository = new SqlUserRepository(connectionString);
        ProcessScheduledTasks(); // Otomatik işlemleri kontrol et ve uygula

        // 2. Kullanıcının profil bilgilerini çek (Ad, Soyad vb.)
        var userProfile = userRepository.GetUserProfile(_currentUser.UserId);

        // 3. İsim var mı kontrol et ve yazdır
        if (userProfile != null && (!string.IsNullOrEmpty(userProfile.FirstName) || !string.IsNullOrEmpty(userProfile.LastName)))
        {
            // Ad ve Soyadı birleştirip yaz (Ör: Ahmet Yılmaz)
            WelcomeText.Text = $"{userProfile.FirstName} {userProfile.LastName}";
        }
        else
        {
            // Eğer profil boşsa veya isim girilmemişse, eski usül E-posta göster
            WelcomeText.Text = _currentUser.Email;
        }
        NavigateTo(new SummaryView(_currentUser.UserId));
        //bool isDark = Properties.Settings.Default.IsDarkMode;
        bool isDark = Properties.Settings.Default.IsDarkMode;
        ModifyTheme(isDark);
        ThemeToggle.IsChecked = isDark;
        CheckScreenResolution();
        if (_currentUser.IsAdmin)
        {
            AdminPanelButton.Visibility = Visibility.Visible;
        }
    }
    
    private void NavigateTo(UserControl newPage)
    {
        MainContentArea.Content = null;
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        MainContentArea.Content = newPage;
    }
    
    private void Logout_Button_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new Pages.MainWindow();
        loginWindow.Show();
        this.Close();
    }
    
    private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new AdminView()); 
    }

    private void SummaryButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new SummaryView(_currentUser.UserId));
    }

    private void IncomeExpenseButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new IncomeExpenseView(_currentUser.UserId));
    }

    private void AnalysisButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new AnalysisView(_currentUser.UserId));
    }

    private void InvestmentsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new InvestmentsView(_currentUser.UserId));
    }

    private void GoalsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateTo(new GoalsView(_currentUser.UserId));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(new ProfileView(_currentUser.UserId, _currentUser.Email));
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
    private void ModifyTheme(bool isDarkTheme)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        // Temayı ayarla (Dark veya Light)
        theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
    
        // Uygula
        paletteHelper.SetTheme(theme);
    }
    
    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        // ToggleButton'ın o anki durumu (True = Dark, False = Light)
        bool isDark = ThemeToggle.IsChecked == true;

        // Temayı değiştir
        ModifyTheme(isDark);

        // Ayarı hafızaya kaydet
        Properties.Settings.Default.IsDarkMode = isDark;
        Properties.Settings.Default.Save();
        ThemeChanged?.Invoke();
    }

    private void ProcessScheduledTasks()
    {
        try
        {
            // 1. ADIM: Otomatik Gelir/Gider İşlemleri
            var autoTransactions = _userRepository.GetAutoTransactions(_currentUser.UserId);
            foreach (var auto in autoTransactions)
            {
                // SAATİ BOŞVER, SADECE TARİHİ KARŞILAŞTIR (.Date)
                while (auto.NextRunDate.Date <= DateTime.Now.Date)
                {
                    var newTrans = new Transaction
                    {
                        UserId = auto.UserId,
                        Amount = auto.Amount,
                        Type = auto.Type,
                        Category = auto.Category,
                        Description = $"{auto.Description} (Otomatik)",
                        TransactionDate = auto.NextRunDate.Date
                    };
                    _userRepository.AddTransaction(newTrans);

                    // Periyodu ilerlet
                    auto.NextRunDate = CalculateNextRunDate(auto.NextRunDate, auto.Frequency);
                }
                // Veritabanını döngü bittikten sonra 1 kere güncelle (Spam'ı engeller)
                _userRepository.UpdateAutoTransactionNextRun(auto.AutoId, auto.NextRunDate);
            }

            // 2. ADIM: Hedeflere Otomatik Para Aktarımı
            var goalAutomations = _userRepository.GetGoalAutomations(_currentUser.UserId);
            foreach (var goalAuto in goalAutomations)
            {
                while (goalAuto.NextRunDate.Date <= DateTime.Now.Date)
                {
                    _userRepository.UpdateGoalAmount(goalAuto.GoalId, goalAuto.Amount);

                    var goalTrans = new Transaction
                    {
                        UserId = goalAuto.UserId,
                        Amount = goalAuto.Amount,
                        Type = "Expense",
                        Category = "Birikim",
                        Description = $"{goalAuto.GoalTitle} Hedefi İçin Otomatik Aktarım",
                        TransactionDate = goalAuto.NextRunDate.Date
                    };
                    _userRepository.AddTransaction(goalTrans);

                    goalAuto.NextRunDate = CalculateNextRunDate(goalAuto.NextRunDate, goalAuto.Frequency);
                }
                _userRepository.UpdateGoalAutomationNextRun(goalAuto.AutomationId, goalAuto.NextRunDate);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Otomatik işlemler sırasında hata: " + ex.Message);
        }
    }

    // Tarih hesaplama yardımcı metodu
    private DateTime CalculateNextRunDate(DateTime currentDate, string frequency)
    {
        return frequency switch
        {
            "Daily" => currentDate.AddDays(1),
            "Weekly" => currentDate.AddDays(7),
            "Monthly" => currentDate.AddMonths(1),
            _ => currentDate.AddMonths(1)
        };
    }
}