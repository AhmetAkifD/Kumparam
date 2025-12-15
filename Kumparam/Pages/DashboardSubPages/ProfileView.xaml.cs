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

        // Constructor senin yapına sadık kalarak email alıyor
        public ProfileView(Guid userId, string email)
        {
            InitializeComponent();
            _currentUserId = userId;
            _userEmail = email;

            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);

            LoadProfile();
        }

        public ProfileView()
        {
            InitializeComponent();
        }

        private void LoadProfile()
        {
            try
            {
                // E-posta bilgisini sol taraftaki karta yaz
                TxtEmailDisplay.Text = _userEmail;

                var profile = _userRepository.GetUserProfile(_currentUserId);
                
                // Inputlara veriyi doldur
                FirstNameTextBox.Text = profile.FirstName;
                LastNameTextBox.Text = profile.LastName;

                // --- SOL KART GÜNCELLEME ---
                
                // Tam İsim
                string fullName = $"{profile.FirstName} {profile.LastName}".Trim();
                TxtFullName.Text = string.IsNullOrEmpty(fullName) ? "İsimsiz Kullanıcı" : fullName;

                // Baş Harfler (Avatar)
                // Örn: Ali Veli -> AV
                string initials = "";
                if (!string.IsNullOrEmpty(profile.FirstName)) initials += profile.FirstName[0];
                if (!string.IsNullOrEmpty(profile.LastName)) initials += profile.LastName[0];
                
                TxtInitials.Text = string.IsNullOrEmpty(initials) ? "?" : initials.ToUpper();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Profil yüklenirken hata: " + ex.Message);
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
                
                // Karttaki ismi ve baş harfleri yenilemek için tekrar yükle
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
                
                // PasswordHelper senin projedeki class, aynen kullanıyoruz
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
                MessageBoxImage.Error); // Warning yerine Error ikonu daha ciddi durur
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _userRepository.ResetUserData(_currentUserId);
                    MessageBox.Show("Hesabınız temizlendi. Temiz bir başlangıç! 🧹");
                    
                    // Veriler silindiği için Dashboard'daki diğer grafiklerin de yenilenmesi gerekebilir.
                    // Şimdilik sadece mesaj veriyoruz.
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hata: " + ex.Message);
                }
            }
        }

        private void OpenRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = Window.GetWindow(this) as DashboardWindow;
            if (dashboard != null)
            {
                // RecycleBinView'in constructor'ı senin yapında sadece userId alıyor
                // (Eğer User nesnesi alıyorsa burayı güncellemen gerekebilir)
                dashboard.MainContentArea.Content = new RecycleBinView(_currentUserId);
            }
        }
    }
}