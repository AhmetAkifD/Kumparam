using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;

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
            if (currentAmount > 0)
            {
                var summary = _userRepository.GetFinancialSummary(_currentUserId);
                if (currentAmount > summary.TotalBalance)
                {
                    MessageBox.Show($"Yetersiz Bakiye!\n\nMevcut Bakiyeniz: {summary.TotalBalance:N2} ₺\nHedefe Yatırmak İstediğiniz: {currentAmount:N2} ₺", 
                        "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // İşlemi durdur
                }
            }
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
            
            if (currentAmount > 0)
            {
                var transaction = new Transaction
                {
                    UserId = _currentUserId,
                    Amount = currentAmount,
                    Type = "Expense", // Gider
                    Category = "Birikim",
                    Description = $"Hedef Başlangıcı: {newGoal.Title}",
                    TransactionDate = DateTime.Now
                };
                _userRepository.AddTransaction(transaction);
            }
            
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
    private void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        // DİKKAT: Artık btn.Tag bize 'Goal' nesnesi veriyor (Guid değil)
        if (sender is Button btn && btn.Tag is Goal goal)
        {
            var result = MessageBox.Show($"'{goal.Title}' hedefini silmek istediğinize emin misiniz?\n" +
                                         $"(İçindeki {goal.CurrentAmount:N2} ₺ bakiyenize iade edilecek.)", 
                "Silme ve İade Onayı", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. İÇİNDE PARA VARSA İADE ET (GELİR EKLE)
                    if (goal.CurrentAmount > 0)
                    {
                        var transaction = new Transaction
                        {
                            UserId = _currentUserId,
                            Amount = goal.CurrentAmount,
                            Type = "Income", // Gelir (Para geri dönüyor)
                            Category = "Birikim İadesi",
                            Description = $"Hedef İptali: {goal.Title} (İade)",
                            TransactionDate = DateTime.Now
                        };
                        _userRepository.AddTransaction(transaction);
                    }

                    // 2. HEDEFİ SİL
                    _userRepository.DeleteGoal(goal.GoalId);
                
                    // 3. LİSTEYİ YENİLE
                    LoadGoals();
                    MessageBox.Show("Hedef silindi ve bakiye hesabınıza iade edildi. 💸");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Silme hatası: {ex.Message}");
                }
            }
        }
    }

    private void AddMoney_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Goal goal)
        {
            // 1. Miktar Sor
            var inputDialog = new SimpleInputWindow("Eklenecek Tutar:");
            if (inputDialog.ShowDialog() == true)
            {
                if (decimal.TryParse(inputDialog.ResultText, out decimal amount) && amount > 0)
                {
                    // 2. Bakiye Kontrolü
                    var summary = _userRepository.GetFinancialSummary(_currentUserId);
                    if (amount > summary.TotalBalance)
                    {
                        MessageBox.Show($"Yetersiz Bakiye!\n\nMevcut: {summary.TotalBalance:N2} ₺\nEklemek İstediğiniz: {amount:N2} ₺", 
                                        "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        // 3. Hedefe Para Ekle (Update)
                        // Not: UpdateGoalAmount metodu mevcut miktarın üzerine ekleme yapacak şekilde ayarlanmıştı.
                        _userRepository.UpdateGoalAmount(goal.GoalId, amount);

                        // 4. Gider Fişi Kes (Bakiyeden Düş)
                        var transaction = new Transaction
                        {
                            UserId = _currentUserId,
                            Amount = amount,
                            Type = "Expense", // Gider
                            Category = "Birikim",
                            Description = $"Hedefe Ekleme: {goal.Title}",
                            TransactionDate = DateTime.Now
                        };
                        _userRepository.AddTransaction(transaction);

                        MessageBox.Show($"{amount:N0} ₺ hedefe eklendi ve bakiyeden düşüldü! 🎯");
                        LoadGoals();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"İşlem hatası: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("Geçersiz miktar.");
                }
            }
        }
    }
}