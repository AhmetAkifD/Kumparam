using System;
using System.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media; // Renkler için (Brush)
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages
{
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
                        return;
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
                
                TitleTextBox.Clear();
                TargetAmountTextBox.Clear();
                CurrentAmountTextBox.Text = "0";
                DescriptionTextBox.Clear();
                DeadlineDatePicker.SelectedDate = null;

                LoadGoals();
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
                        if (goal.CurrentAmount > 0)
                        {
                            var transaction = new Transaction
                            {
                                UserId = _currentUserId,
                                Amount = goal.CurrentAmount,
                                Type = "Income", // Gelir (Para geri dönüyor)
                                Category = "Birikim",
                                Description = $"Hedef İptali: {goal.Title} (İade)",
                                TransactionDate = DateTime.Now
                            };
                            _userRepository.AddTransaction(transaction);
                        }

                        _userRepository.DeleteGoal(goal.GoalId);
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
                var inputDialog = new SimpleInputWindow("Eklenecek Tutar:");
                if (inputDialog.ShowDialog() == true)
                {
                    if (decimal.TryParse(inputDialog.ResultText, out decimal amount) && amount > 0)
                    {
                        var summary = _userRepository.GetFinancialSummary(_currentUserId);
                        if (amount > summary.TotalBalance)
                        {
                            MessageBox.Show($"Yetersiz Bakiye!\n\nMevcut: {summary.TotalBalance:N2} ₺\nEklemek İstediğiniz: {amount:N2} ₺", 
                                            "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        try
                        {
                            _userRepository.UpdateGoalAmount(goal.GoalId, amount);

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

        private void RemoveMoney_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Goal goal)
            {
                var inputDialog = new SimpleInputWindow("Geri Alınacak Tutar:");
                if (inputDialog.ShowDialog() == true)
                {
                    if (decimal.TryParse(inputDialog.ResultText, out decimal amount) && amount > 0)
                    {
                        if (amount > goal.CurrentAmount)
                        {
                            MessageBox.Show($"Hedefte bu kadar para yok!\n\nHedef Bakiyesi: {goal.CurrentAmount:N2} ₺\nÇekmek İstenen: {amount:N2} ₺", 
                                            "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        try
                        {
                            _userRepository.UpdateGoalAmount(goal.GoalId, -amount);

                            var transaction = new Transaction
                            {
                                UserId = _currentUserId,
                                Amount = amount,
                                Type = "Income", // GELİR olarak eklenir
                                Category = "Birikim",
                                Description = $"Hedeften Çekim: {goal.Title}",
                                TransactionDate = DateTime.Now
                            };
                            _userRepository.AddTransaction(transaction);

                            MessageBox.Show($"{amount:N0} ₺ hedeften çekildi ve ana bakiyeye eklendi! 💸");
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

    // 1. Yazı Dönüştürücü: "3 Gün Kaldı" veya "5 Gün Gecikti"
    public class DeadlineToRemainingDaysConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime deadline)
            {
                // Ceiling ile yukarı yuvarlıyoruz ki "0.1 gün" bile olsa "1 gün" desin
                var difference = deadline.Date - DateTime.Now.Date;
                int days = (int)difference.TotalDays;
                
                if (days < 0) return $"{Math.Abs(days)} Gün Gecikti!";
                if (days == 0) return "Bugün Son Gün!";
                
                return $"{days} Gün Kaldı";
            }
            return ""; 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 2. Renk Dönüştürücü: Gecikirse Kırmızı, Bugünse Turuncu, Varsa Mavi
    public class DeadlineToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime deadline)
            {
                var days = (int)(deadline.Date - DateTime.Now.Date).TotalDays;

                if (days < 0) return Brushes.Red;           // Gecikti
                if (days == 0) return Brushes.OrangeRed;    // Bugün son
                if (days <= 7) return Brushes.DarkGoldenrod;// Az kaldı (Son 1 hafta)
                
                return Brushes.DodgerBlue; // Daha vakit var
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}