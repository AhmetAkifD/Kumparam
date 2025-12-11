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
            EmailTextBox.Text = _userEmail;
            var profile = _userRepository.GetUserProfile(_currentUserId);
            FirstNameTextBox.Text = profile.FirstName;
            LastNameTextBox.Text = profile.LastName;
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
            MessageBox.Show("Profil güncellendi! ✅");
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
            MessageBox.Show("Lütfen alanları doldurun.");
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
        var result = MessageBox.Show("DİKKAT! Tüm verileriniz silinecek. Emin misiniz?", "Veri Sıfırlama", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _userRepository.ResetUserData(_currentUserId);
                MessageBox.Show("Hesabınız temizlendi. Temiz bir başlangıç! 🧹");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }
    }
}