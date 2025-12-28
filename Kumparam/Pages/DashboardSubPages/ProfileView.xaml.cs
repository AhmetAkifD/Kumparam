using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Linq; // String işlemleri için
using Kumparam.Core; 
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages
{
    public partial class ProfileView : UserControl
    {
        private readonly IUserRepository _userRepository;
        private readonly Guid _currentUserId;
        private readonly string _userEmail;

        // Constructor
        public ProfileView(Guid userId, string email)
        {
            InitializeComponent();
            _currentUserId = userId;
            _userEmail = email;

            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);

            // İlk açılışta verileri yükle
            LoadProfile();
        }

        public ProfileView()
        {
            InitializeComponent();
        }

        private void LoadProfile()
        {
            if (_currentUserId == Guid.Empty) return;

            try
            {
                TxtEmailDisplay.Text = _userEmail;
                var profile = _userRepository.GetUserProfile(_currentUserId);
                
                if (profile != null)
                {
                    FirstNameTextBox.Text = profile.FirstName;
                    LastNameTextBox.Text = profile.LastName;

                    // --- SOL KART GÜNCELLEME ---
                    if (_userEmail.ToLower().Contains("admin")) 
                    {
                        TxtUserRole.Text = "Yönetici";
                        TxtUserRole.Foreground = System.Windows.Media.Brushes.Red; // Admin kırmızı görünsün
                    }
                    else
                    {
                        TxtUserRole.Text = "Standart Üye";
                        // TxtUserRole.Foreground = System.Windows.Media.Brushes.Gray;
                    }
                    string fullName = $"{profile.FirstName} {profile.LastName}".Trim();
                    TxtFullName.Text = string.IsNullOrEmpty(fullName) ? "İsimsiz Kullanıcı" : fullName;

                    // Avatar (Baş Harfler)
                    string initials = "";
                    if (!string.IsNullOrEmpty(profile.FirstName)) initials += profile.FirstName[0];
                    if (!string.IsNullOrEmpty(profile.LastName)) initials += profile.LastName[0];
                    
                    TxtInitials.Text = string.IsNullOrEmpty(initials) ? "?" : initials.ToUpper();
                }
            }
            catch (Exception ex)
            {
                // Hata olursa sessizce yutabilir veya loglayabilirsin
            }
        }

        private void UpdateProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var updatedProfile = new UserProfile
                {
                    UserId = _currentUserId,
                    FirstName = FirstNameTextBox.Text,
                    LastName = LastNameTextBox.Text
                };
                _userRepository.UpdateUserProfile(updatedProfile);
                
                MessageBox.Show("Profil bilgileriniz güncellendi! ✅");
                LoadProfile();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string oldPass = OldPasswordBox.Password;
            string newPass = NewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(oldPass) || string.IsNullOrWhiteSpace(newPass))
            {
                MessageBox.Show("Lütfen tüm alanları doldurun.");
                return;
            }

            try
            {
                var user = _userRepository.GetUserById(_currentUserId);
                
                if (user != null && PasswordHelper.VerifyPassword(oldPass, user.PasswordSalt, user.PasswordHash))
                {
                    PasswordHelper.HashPassword(newPass, out byte[] newSalt, out byte[] newHash);
                    _userRepository.UpdatePassword(_currentUserId, newHash, newSalt);
                    
                    MessageBox.Show("Şifre başarıyla değiştirildi! 🔒");
                    OldPasswordBox.Clear();
                    NewPasswordBox.Clear();
                }
                else
                {
                    MessageBox.Show("Mevcut şifre hatalı! ❌");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void ResetData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "DİKKAT! Tüm işlemler, yatırımlar ve hedefler silinecek.\n\nBu işlem geri alınamaz. Emin misiniz?", 
                "Veri Sıfırlama", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Error); 
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _userRepository.ResetUserData(_currentUserId);
                    MessageBox.Show("Hesabınız temizlendi. 🧹");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hata: " + ex.Message);
                }
            }
        }

        private void OpenRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            // ÇÖZÜM: Geri Dönüşüm Kutusunu AYRI BİR PENCERE (Dialog) olarak açıyoruz.
            
            // 1. Yeni, geçici bir pencere oluştur
            Window recycleWindow = new Window
            {
                Title = "Geri Dönüşüm Kutusu",
                // Mevcut UserControl'ü bu pencerenin içine koyuyoruz
                Content = new RecycleBinView(_currentUserId), 
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                // İstersen pencere boyutlandırmasını kapatabilirsin:
                // ResizeMode = ResizeMode.NoResize
            };

            // 2. Pencereyi MODAL olarak aç.
            // Bu satır çalıştığında, kullanıcı pencereyi kapatana kadar kod burada DURUR.
            recycleWindow.ShowDialog();

            // 3. Pencere kapandığında kod buradan devam eder ve profili yeniler.
            // Böylece sildiğin veriler geri yüklendiyse anında yansır.
            LoadProfile();
        }
        private void ToggleOldPassword_Click(object sender, RoutedEventArgs e)
        {
            var toggleButton = sender as System.Windows.Controls.Primitives.ToggleButton;
            bool goster = toggleButton.IsChecked == true;

            if (goster)
            {
                OldPasswordVisibleBox.Text = OldPasswordBox.Password;
                OldPasswordBox.Visibility = Visibility.Collapsed;
                OldPasswordVisibleBox.Visibility = Visibility.Visible;
            }
            else
            {
                OldPasswordBox.Password = OldPasswordVisibleBox.Text;
                OldPasswordVisibleBox.Visibility = Visibility.Collapsed;
                OldPasswordBox.Visibility = Visibility.Visible;
            }
        }

        private void ToggleNewPassword_Click(object sender, RoutedEventArgs e)
        {
            var toggleButton = sender as System.Windows.Controls.Primitives.ToggleButton;
            bool goster = toggleButton.IsChecked == true;

            if (goster)
            {
                NewPasswordVisibleBox.Text = NewPasswordBox.Password;
                NewPasswordBox.Visibility = Visibility.Collapsed;
                NewPasswordVisibleBox.Visibility = Visibility.Visible;
            }
            else
            {
                NewPasswordBox.Password = NewPasswordVisibleBox.Text;
                NewPasswordVisibleBox.Visibility = Visibility.Collapsed;
                NewPasswordBox.Visibility = Visibility.Visible;
            }
        }
        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "DİKKAT! Hesabınız tamamen silinecek. Bu işlem geri alınamaz!\n", 
                "Hesap Silme", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // İkinci bir onay daha alalım (Çok kritik işlem)
                var finalConfirm = MessageBox.Show(
                    "Verilerinizin silinmesi için bir kere daha onaylayınız.", 
                    "Son Onay", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (finalConfirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 1. Veritabanından sil
                        // Eğer Repository'de DeleteUser yoksa eklememiz lazım!
                        _userRepository.DeleteUser(_currentUserId); 

                        MessageBox.Show("Hesabınız silindi.");

                        // 2. Dashboard'ı kapat ve Login ekranını aç
                        // UserControl'ün içinde olduğumuz için Parent Window'u bulmamız lazım
                        Window parentWindow = Window.GetWindow(this);
                
                        MainWindow loginWindow = new MainWindow();
                        loginWindow.Show();
                
                        parentWindow?.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Silme işlemi sırasında hata oluştu: " + ex.Message);
                    }
                }
            }
        }
    }
}