using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Core.Models; 
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages;

public partial class ProfileView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    private readonly string _userEmail;

    // Parametreli Constructor (Dashboard'dan çağrılır)
    public ProfileView(Guid userId, string email)
    {
        InitializeComponent();
        _currentUserId = userId;
        _userEmail = email;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        LoadProfile();
    }

    // Boş Constructor (XAML Designer için)
    public ProfileView()
    {
        InitializeComponent();
    }

    private void LoadProfile()
    {
        try
        {
            // E-postayı ekrana yaz
            EmailTextBox.Text = _userEmail;

            // Profil bilgilerini çek
            var profile = _userRepository.GetUserProfile(_currentUserId);
            FirstNameTextBox.Text = profile.FirstName;
            LastNameTextBox.Text = profile.LastName;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Profil yüklenirken hata oluştu: " + ex.Message, "Hata");
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
            MessageBox.Show("Profil bilgileriniz güncellendi. ✅", "Başarılı");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Güncelleme başarısız: " + ex.Message, "Hata");
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        string oldPassword = OldPasswordBox.Password;
        string newPassword = NewPasswordBox.Password;

        // 1. Validasyon
        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            MessageBox.Show("Lütfen mevcut ve yeni şifrenizi girin.", "Eksik Bilgi");
            return;
        }

        if (newPassword.Length < 4) // Örnek kural
        {
            MessageBox.Show("Yeni şifre en az 4 karakter olmalıdır.", "Zayıf Şifre");
            return;
        }

        try
        {
            // 2. Kullanıcı bilgilerini (Mevcut Hash/Salt) çek
            var user = _userRepository.GetUserById(_currentUserId);
            if (user == null)
            {
                MessageBox.Show("Kullanıcı bulunamadı.", "Hata");
                return;
            }

            // 3. Eski şifreyi doğrula
            bool isOldPasswordCorrect = PasswordHelper.VerifyPassword(oldPassword, user.PasswordSalt, user.PasswordHash);
            if (!isOldPasswordCorrect)
            {
                MessageBox.Show("Mevcut şifreniz hatalı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. Yeni şifreyi Hash'le
            PasswordHelper.HashPassword(newPassword, out byte[] newSalt, out byte[] newHash);

            // 5. Veritabanını güncelle
            _userRepository.UpdatePassword(_currentUserId, newHash, newSalt);

            MessageBox.Show("Şifreniz başarıyla değiştirildi! 🔒", "Başarılı");
            
            // Kutuları temizle
            OldPasswordBox.Clear();
            NewPasswordBox.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Şifre değiştirme hatası: " + ex.Message, "Hata");
        }
    }

    private void ResetData_Click(object sender, RoutedEventArgs e)
    {
        // Standart MessageBox ile Onay Alma
        var result = MessageBox.Show(
            "DİKKAT! Tüm gelir, gider, yatırım ve hedefleriniz silinecek.\nBu işlem geri alınamaz. Emin misiniz?", 
            "Verileri Sıfırla", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _userRepository.ResetUserData(_currentUserId);
                MessageBox.Show("Tüm verileriniz sıfırlandı. Temiz bir sayfa açtınız! 🧹", "Başarılı");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Sıfırlama başarısız: " + ex.Message, "Hata");
            }
        }
    }
}