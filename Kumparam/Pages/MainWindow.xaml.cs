using System.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Kumparam.Data;
using Kumparam.Data.Repositories;
using Kumparam.Data.Helpers;
using System.Text.RegularExpressions;


namespace Kumparam.Pages;
public partial class MainWindow : Window
{
    private readonly IUserRepository _userRepository;
    public MainWindow()
    {
        InitializeComponent();                              
        try
        {
            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);
            try 
            {
                var dbInit = new DatabaseInitializer(connectionString);
                dbInit.Initialize();
                _userRepository = new SqlUserRepository(connectionString);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veritabanı oluşturulamadı.\n" + ex.Message);
            }
            if (_userRepository.IsConnectionSuccess())
            {
                DbConnectionStatusTextBlock.Text = "✅ Veritabanı Bağlantısı Başarılı";
                DbConnectionStatusTextBlock.Foreground = Brushes.Green;
            }
            else
            {
                DbConnectionStatusTextBlock.Text = "❌ Veritabanı Bağlantı Hatası!";
                DbConnectionStatusTextBlock.Foreground = Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            _userRepository = null!;
            DbConnectionStatusTextBlock.Text = "❌ Kritik Hata: " + ex.Message;
            DbConnectionStatusTextBlock.Foreground = Brushes.Red;
        }
        // TEMA AYARINI YÜKLE
        // Daha önce kaydedilen ayarı okuyoruz
        bool isDark = Kumparam.Properties.Settings.Default.IsDarkMode;
    
        // Toggle butonunun durumunu güncelle (Checked = Dark Mode)
        ThemeToggle.IsChecked = isDark;
    
        // Uygulamanın temasını ayarla
        App.ModifyTheme(isDark);
    }
    
    private async void KayitEkraniniGoster_Click(object sender, RoutedEventArgs e)
    {
        LoginGrid.IsHitTestVisible = false;
        RegisterGrid.IsHitTestVisible = false;
        
        await SlideAsync(fromGrid: LoginGrid, toGrid: RegisterGrid, leftToRight: true);
        
        LoginGrid.IsHitTestVisible = true;
        RegisterGrid.IsHitTestVisible = true;
    }

    private async void GirisEkraniniGoster_Click(object sender, RoutedEventArgs e)
    {
        LoginGrid.IsHitTestVisible = false;
        RegisterGrid.IsHitTestVisible = false;

        await SlideAsync(fromGrid: RegisterGrid, toGrid: LoginGrid, leftToRight: false);
        
        LoginGrid.IsHitTestVisible = true;
        RegisterGrid.IsHitTestVisible = true;
    }
    
    private void Register_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_userRepository == null) return;
        var email = Register_EmailTextBox.Text;
        var sifre = Register_SifrePasswordBox.Password;
        var sifreTekrar = Register_SifreTekrarPasswordBox.Password;
        var isim = Register_IsimTextBox.Text;
        var soyisim = Register_SoyisimTextBox.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sifre) || 
            string.IsNullOrWhiteSpace(isim) || string.IsNullOrWhiteSpace(soyisim))
        {
            MessageBox.Show("Tüm alanlar doldurulmalıdır."); 
            return;
        }
        string emailPattern = @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$";
        if (!Regex.IsMatch(email, emailPattern))
        {
            MessageBox.Show("Lütfen geçerli bir e-posta adresi giriniz (ör: ornek@mail.com).");
            return;
        }
        if (sifre != sifreTekrar)
        {
            MessageBox.Show("Şifreler eşleşmiyor.");
            return;
        }

        try
        {
            if (_userRepository.EmailExists(email))
            {
                MessageBox.Show("Bu e-posta adresi zaten kullanılıyor.");
                return;
            }
            PasswordHelper.HashPassword(sifre, out byte[] salt, out byte[] hash);
            var userId = Guid.NewGuid();
            var newUser = new User
            {
                UserId = userId,
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt
            };
            
            var newUserProfile = new UserProfile
            {
                UserId = userId,
                FirstName = isim,
                LastName = soyisim
            };

            _userRepository.AddUser(newUser, newUserProfile);

            MessageBox.Show("Hesap başarıyla oluşturuldu! Şimdi giriş yapabilirsiniz.");
            
            GirisEkraniniGoster_Click(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Bir hata oluştu: {ex.Message}. Detay: {ex.InnerException?.Message}");
        }
    }
    private void SifreGosterGizle_Click(object sender, RoutedEventArgs e)
    {
        var toggleButton = sender as System.Windows.Controls.Primitives.ToggleButton;
        bool goster = toggleButton.IsChecked == true;

        if (goster)
        {
            SifreAcikTextBox.Text = SifrePasswordBox.Password;
            SifrePasswordBox.Visibility = Visibility.Collapsed;
            SifreAcikTextBox.Visibility = Visibility.Visible;
        }
        else
        {
            SifrePasswordBox.Password = SifreAcikTextBox.Text;
            SifreAcikTextBox.Visibility = Visibility.Collapsed;
            SifrePasswordBox.Visibility = Visibility.Visible;
        }
    }

    private void Login_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_userRepository == null) 
        { 
            MessageBox.Show("Veritabanı bağlantısı yok."); 
            return; 
        }

        var email = EmailTextBox.Text;
        var sifre = SifrePasswordBox.Password;
        
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sifre))
        {
            HataMesajiTextBlock.Text = "Lütfen e-posta ve şifrenizi girin.";
            HataMesajiTextBlock.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var user = _userRepository.GetUserByEmail(email);
            bool girisBasarili = false;

            if (user != null)
            {
                if (PasswordHelper.VerifyPassword(sifre, user.PasswordSalt, user.PasswordHash))
                {
                    girisBasarili = true;
                }
            }
            if (girisBasarili)
            {
                HataMesajiTextBlock.Visibility = Visibility.Collapsed;
                HataMesajiTextBlock.Visibility = Visibility.Collapsed;
                var dashboard = new DashboardWindow(user!); 
                dashboard.Show();
                this.Close();
            }
            else
            {
                HataMesajiTextBlock.Text = "E-posta veya şifre hatalı!";
                HataMesajiTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Giriş sırasında bir hata oluştu: {ex.Message}");
        }
    }
    
    private void OpenDashboard(User user)
    {
        HataMesajiTextBlock.Visibility = Visibility.Collapsed;
        var dashboard = new DashboardWindow(user); 
        dashboard.Show();
        this.Close(); 
    }
    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Login_Button_Click(sender, e);
        }
    }

    private void PasswordAgain_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Register_Button_Click(sender, e);
        }
    }
    // 1. Register ekranındaki ilk Şifre kutusu için
    private void Register_SifreGoster_Click(object sender, RoutedEventArgs e)
    {
        var toggleButton = sender as System.Windows.Controls.Primitives.ToggleButton;
        bool goster = toggleButton.IsChecked == true;

        if (goster)
        {
            Register_SifreAcikTextBox.Text = Register_SifrePasswordBox.Password;
            Register_SifrePasswordBox.Visibility = Visibility.Collapsed;
            Register_SifreAcikTextBox.Visibility = Visibility.Visible;
        }
        else
        {
            Register_SifrePasswordBox.Password = Register_SifreAcikTextBox.Text;
            Register_SifreAcikTextBox.Visibility = Visibility.Collapsed;
            Register_SifrePasswordBox.Visibility = Visibility.Visible;
        }
    }

// 2. Register ekranındaki Şifre Tekrar kutusu için
    private void Register_SifreTekrarGoster_Click(object sender, RoutedEventArgs e)
    {
        var toggleButton = sender as System.Windows.Controls.Primitives.ToggleButton;
        bool goster = toggleButton.IsChecked == true;

        if (goster)
        {
            Register_SifreTekrarAcikTextBox.Text = Register_SifreTekrarPasswordBox.Password;
            Register_SifreTekrarPasswordBox.Visibility = Visibility.Collapsed;
            Register_SifreTekrarAcikTextBox.Visibility = Visibility.Visible;
        }
        else
        {
            Register_SifreTekrarPasswordBox.Password = Register_SifreTekrarAcikTextBox.Text;
            Register_SifreTekrarAcikTextBox.Visibility = Visibility.Collapsed;
            Register_SifreTekrarPasswordBox.Visibility = Visibility.Visible;
        }
    }

    private Task SlideAsync(UIElement fromGrid, UIElement toGrid, bool leftToRight)
    {
        var tcs = new TaskCompletionSource<bool>();

        double width = MainGrid.ActualWidth;
        if (width <= 0) width = this.Width;
        if (fromGrid.RenderTransform == null || !(fromGrid.RenderTransform is TranslateTransform))
            fromGrid.RenderTransform = new TranslateTransform(0, 0);
        if (toGrid.RenderTransform == null || !(toGrid.RenderTransform is TranslateTransform))
            toGrid.RenderTransform = new TranslateTransform(0, 0);

        var fromTrans = (TranslateTransform)fromGrid.RenderTransform;
        var toTrans = (TranslateTransform)toGrid.RenderTransform;
        toGrid.Visibility = Visibility.Visible;
        if (leftToRight)
        {
            toTrans.X = width;
        }
        else
        {
            toTrans.X = -width;
        }

        var duration = TimeSpan.FromMilliseconds(600);
        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut }; 
        var animFrom = new DoubleAnimation
        {
            To = leftToRight ? -width : width,
            Duration = new Duration(duration),
            EasingFunction = easing
        };
        var animTo = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };
        void OnCompleted(object? s, EventArgs e)
        {
            fromGrid.Visibility = Visibility.Collapsed;
            fromTrans.X = 0;
            toTrans.X = 0;
            animTo.Completed -= OnCompleted;
            tcs.TrySetResult(true);
        }
        animTo.Completed += OnCompleted;
        fromTrans.BeginAnimation(TranslateTransform.XProperty, animFrom);
        toTrans.BeginAnimation(TranslateTransform.XProperty, animTo);

        return tcs.Task;
    }
    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        // Toggle basılıysa (True) -> Karanlık Mod, değilse -> Aydınlık Mod
        bool isDark = ThemeToggle.IsChecked == true;

        // 1. Temayı değiştir
        App.ModifyTheme(isDark);

        // 2. Ayarı hafızaya kaydet
        Kumparam.Properties.Settings.Default.IsDarkMode = isDark;
        Kumparam.Properties.Settings.Default.Save();
    }
}