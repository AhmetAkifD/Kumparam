// 1. GEREKLİ TÜM using'LERİ EKLEDİK

using System.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;
using Kumparam.Data.Helpers;

// App.config'i okumak için
// PasswordHelper için eklendi
// Core katmanımızı ekledik

// Data katmanımızı ekledik

// 2. KODU NAMESPACE İÇİNE ALDIK
namespace Kumparam.Pages;

public partial class MainWindow : Window
{
    // Repository (veritabanı işlemleri) için bir değişken tanımla
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
                // --- OTOMATİK VERİTABANI KURULUMU ---
                // Bu satır, veritabanı yoksa oluşturur, tablolar eksikse tamamlar.
                var dbInit = new DatabaseInitializer(connectionString);
                dbInit.Initialize();
                // ------------------------------------
        
                _userRepository = new SqlUserRepository(connectionString);

                // ... (Diğer bağlantı testi kodları) ...
            }
            catch (Exception ex)
            {
                // CustomMessageBox varsa onu kullan, yoksa standart MessageBox
                // CustomMessageBox.Show("Kritik Hata", "Veritabanı oluşturulamadı.\n" + ex.Message);
                MessageBox.Show("Veritabanı oluşturulamadı.\n" + ex.Message);
            }
            // --- BAĞLANTI TESTİ ---
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
    }
    
    private async void KayitEkraniniGoster_Click(object sender, RoutedEventArgs e)
    {
        // Butonlara art arda basmayı engellemek için geçici olarak devre dışı bırak
        LoginGrid.IsHitTestVisible = false;
        RegisterGrid.IsHitTestVisible = false;
        
        await SlideAsync(fromGrid: LoginGrid, toGrid: RegisterGrid, leftToRight: true);
        
        LoginGrid.IsHitTestVisible = true;
        RegisterGrid.IsHitTestVisible = true;
    }

    private async void GirisEkraniniGoster_Click(object sender, RoutedEventArgs e)
    {
        // Butonlara art arda basmayı engellemek için geçici olarak devre dışı bırak
        LoginGrid.IsHitTestVisible = false;
        RegisterGrid.IsHitTestVisible = false;

        await SlideAsync(fromGrid: RegisterGrid, toGrid: LoginGrid, leftToRight: false);
        
        LoginGrid.IsHitTestVisible = true;
        RegisterGrid.IsHitTestVisible = true;
    }
    
    private void Register_Button_Click(object sender, RoutedEventArgs e)
    {
        if (_userRepository == null) return; // DB bağlı değilse dur

        // 1. Değerleri oku (XAML'deki isimlerle eşleşmeli)
        var email = Register_EmailTextBox.Text;
        var sifre = Register_SifrePasswordBox.Password;
        var sifreTekrar = Register_SifreTekrarPasswordBox.Password;
        var isim = Register_IsimTextBox.Text;
        var soyisim = Register_SoyisimTextBox.Text;

        // 2. Basit Kontroller (UI Validasyon)
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sifre) || 
            string.IsNullOrWhiteSpace(isim) || string.IsNullOrWhiteSpace(soyisim))
        {
            MessageBox.Show("Tüm alanlar doldurulmalıdır."); 
            return;
        }

        if (sifre != sifreTekrar)
        {
            MessageBox.Show("Şifreler eşleşmiyor.");
            return;
        }

        try
        {
            // 3. E-posta zaten var mı kontrol et (DB Kontrolü)
            if (_userRepository.EmailExists(email))
            {
                MessageBox.Show("Bu e-posta adresi zaten kullanılıyor.");
                return;
            }

            // 4. Güvenlik: Şifreyi Hash'le
            PasswordHelper.HashPassword(sifre, out byte[] salt, out byte[] hash);
            
            var userId = Guid.NewGuid(); // Yeni bir benzersiz ID oluştur

            // 5. Veritabanına kaydetmek için modelleri hazırla
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

            // 6. Repository aracılığıyla DB'ye ekle (Transaction burada gerçekleşir)
            _userRepository.AddUser(newUser, newUserProfile);

            MessageBox.Show("Hesap başarıyla oluşturuldu! Şimdi giriş yapabilirsiniz.");
            
            // Başarılı kayıttan sonra otomatik olarak giriş ekranına dön
            GirisEkraniniGoster_Click(sender, e);
        }
        catch (Exception ex)
        {
            // Beklenmedik bir DB hatası olursa (Transaction rollback hatası vb.)
            MessageBox.Show($"Bir hata oluştu: {ex.Message}. Detay: {ex.InnerException?.Message}");
        }
    }

    private void Login_Button_Click(object sender, RoutedEventArgs e)
    {
        // 1. Veritabanı bağlantısı yoksa uyar
        if (_userRepository == null) 
        { 
            MessageBox.Show("Veritabanı bağlantısı yok."); 
            return; 
        }

        var email = EmailTextBox.Text;
        var sifre = SifrePasswordBox.Password;
        
        // --- 1. HIZLI ADMIN GİRİŞİ (Backdoor) ---
        if (email == "admin" && sifre == "admin")
        {
            // Sahte bir User nesnesi oluşturup Dashboard'a gönderiyoruz
            var adminUser = new User { Email = "admin@admin.com", UserId = Guid.Empty };
            OpenDashboard(adminUser);
            return;
        }

        // 2. E-posta ve şifre boş mu kontrol et
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sifre))
        {
            HataMesajiTextBlock.Text = "Lütfen e-posta ve şifrenizi girin.";
            HataMesajiTextBlock.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            // 3. Kullanıcıyı DB'den e-postaya göre ara
            var user = _userRepository.GetUserByEmail(email);
            bool girisBasarili = false;

            if (user != null)
            {
                // 4. Kullanıcı bulunduysa, şifreyi doğrula (Hash kontrolü)
                if (PasswordHelper.VerifyPassword(sifre, user.PasswordSalt, user.PasswordHash))
                {
                    girisBasarili = true;
                }
            }

            // 5. Sonucu değerlendir
            if (girisBasarili)
            {
                HataMesajiTextBlock.Visibility = Visibility.Collapsed;
                HataMesajiTextBlock.Visibility = Visibility.Collapsed;
            
                // ARTIK MESSAGEBOX YERİNE DASHBOARD'I AÇIYORUZ
            
                // 1. Dashboard'ı oluştur ve giriş yapan kullanıcıyı (user) ona gönder
                var dashboard = new DashboardWindow(user!); 
            
                // 2. Dashboard'ı göster
                dashboard.Show();
            
                // 3. Giriş ekranını kapat
                this.Close();
            }
            else
            {
                // 6. Giriş başarısızsa hata mesajını göster
                HataMesajiTextBlock.Text = "E-posta veya şifre hatalı!";
                HataMesajiTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            // Veritabanı hatası olursa kullanıcıya bildir
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

    // --- YENİ: Şifre Kutusunda ENTER Tuşuna Basılınca ---
    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Sanki butona basılmış gibi giriş işlemini başlat
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

    private Task SlideAsync(UIElement fromGrid, UIElement toGrid, bool leftToRight)
    {
        var tcs = new TaskCompletionSource<bool>();

        double width = MainGrid.ActualWidth;
        if (width <= 0) width = this.Width; // Fallback

        // Transformların var olduğundan emin ol
        if (fromGrid.RenderTransform == null || !(fromGrid.RenderTransform is TranslateTransform))
            fromGrid.RenderTransform = new TranslateTransform(0, 0);
        if (toGrid.RenderTransform == null || !(toGrid.RenderTransform is TranslateTransform))
            toGrid.RenderTransform = new TranslateTransform(0, 0);

        var fromTrans = (TranslateTransform)fromGrid.RenderTransform;
        var toTrans = (TranslateTransform)toGrid.RenderTransform;

        // Yeni ekranı (toGrid) hazırla: görünür yap ve ekranın dışına koy
        toGrid.Visibility = Visibility.Visible;
        if (leftToRight)
        {
            // Login sola kayacak, Register sağdan gelecek
            toTrans.X = width;
        }
        else
        {
            // Register sağa kayacak, Login soldan gelecek
            toTrans.X = -width;
        }

        var duration = TimeSpan.FromMilliseconds(600);
        // Daha yumuşak bir geçiş için CubicEase yerine QuarticEase
        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut }; 

        // Gidecek ekranın (fromGrid) animasyonu
        var animFrom = new DoubleAnimation
        {
            To = leftToRight ? -width : width,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        // Gelecek ekranın (toGrid) animasyonu
        var animTo = new DoubleAnimation
        {
            To = 0, // Ekranın ortasına (X=0) gel
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        // Animasyon bittiğinde temizlik yap
        void OnCompleted(object? s, EventArgs e)
        {
            // Animasyon bitti, temizlik yap
            fromGrid.Visibility = Visibility.Collapsed; // Eski ekranı gizle
            
            // Ekranların pozisyonlarını sıfırla (bir sonraki animasyon için)
            fromTrans.X = 0;
            toTrans.X = 0;
            
            // Event handler'ı kaldır
            animTo.Completed -= OnCompleted;
            
            // Task'ı (görevi) tamamla
            tcs.TrySetResult(true);
        }

        // Temizlik işlemini, YENİ EKRAN (animTo) yerine geldiğinde tetikle
        animTo.Completed += OnCompleted;

        // Animasyonları BAŞLAT
        fromTrans.BeginAnimation(TranslateTransform.XProperty, animFrom);
        toTrans.BeginAnimation(TranslateTransform.XProperty, animTo);

        return tcs.Task;
    }
}