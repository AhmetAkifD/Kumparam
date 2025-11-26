using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kumparam.Core;
using Kumparam.Data;

namespace Kumparam.Pages.DashboardSubPages;

public partial class GoalsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    public GoalsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Artık güvenli olan fonksiyonumuzu çağırıyoruz
        LoadGoals();
    }

    public GoalsView()
    {
        InitializeComponent();
    }

    private void LoadGoals()
    {
        try
        {
            // Bu metot artık SqlUserRepository içinde DÜZELTİLDİĞİ için hata vermeyecek
            var goals = _userRepository.GetGoals(_currentUserId);
            GoalsList.ItemsSource = goals;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}");
        }
    }

    private void SaveGoal_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text) || 
            !decimal.TryParse(TargetAmountTextBox.Text, out decimal targetAmount))
        {
            MessageBox.Show("Başlık ve Tutar zorunludur.");
            return;
        }

        decimal.TryParse(CurrentAmountTextBox.Text, out decimal currentAmount);

        try
        {
            var newGoal = new Goal
            {
                UserId = _currentUserId,
                Title = TitleTextBox.Text,
                TargetAmount = targetAmount,
                CurrentAmount = currentAmount,
                Deadline = DeadlineDatePicker.SelectedDate,
                Description = DescriptionTextBox.Text
            };

            _userRepository.AddGoal(newGoal);
            MessageBox.Show("Hedef Kaydedildi! 🎯");
            
            // Formu Temizle
            TitleTextBox.Clear();
            TargetAmountTextBox.Clear();
            CurrentAmountTextBox.Text = "0";
            DescriptionTextBox.Clear();
            DeadlineDatePicker.SelectedDate = null;

            LoadGoals(); // Listeyi güncelle
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt Hatası: {ex.Message}");
        }
    }

    private void DescriptionTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SaveGoal_Click(sender, e);
    }
}