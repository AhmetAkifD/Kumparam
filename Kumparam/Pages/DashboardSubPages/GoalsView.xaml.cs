using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Kumparam.Data;
using Kumparam.Data.Repositories;
using System;
using System.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media; // Renkler için (Brush)

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

        public GoalsView() { InitializeComponent(); }

        private void LoadGoals()
        {
            try
            {
                var goals = _userRepository.GetGoals(_currentUserId);
                var automations = _userRepository.GetGoalAutomations(_currentUserId);

                foreach (var goal in goals)
                {
                    // Bu hedefe bağlı aktif otomasyonları hesapla
                    var goalAutos = automations.Where(a => a.GoalId == goal.GoalId && a.IsActive).ToList();
                    decimal dailySaving = 0;

                    foreach (var auto in goalAutos)
                    {
                        dailySaving += auto.Frequency switch
                        {
                            "Daily" => auto.Amount,
                            "Weekly" => auto.Amount / 7m,
                            "Monthly" => auto.Amount / 30m,
                            _ => 0
                        };
                    }

                    decimal remainingAmount = goal.TargetAmount - goal.CurrentAmount;

                    if (remainingAmount <= 0)
                    {
                        goal.Description = "✅ Bu hedefe başarıyla ulaşıldı!";
                    }
                    else if (dailySaving > 0)
                    {
                        int daysLeft = (int)Math.Ceiling(remainingAmount / dailySaving);
                        string timeText = FormatTimeLeft(daysLeft);
                        // Tahmin metnini açıklamanın sonuna ekliyoruz (XAML'da tek binding için kolaylık)
                        goal.Description = string.IsNullOrWhiteSpace(goal.Description)
                            ? $"📅 Tahmini: {timeText} sonra ulaşılacak."
                            : $"{goal.Description}\n📅 Tahmini: {timeText} sonra ulaşılacak.";
                    }
                    else
                    {
                        goal.Description = string.IsNullOrWhiteSpace(goal.Description)
                            ? "⚠️ Otomatik birikim tanımlanmamış."
                            : $"{goal.Description}\n⚠️ Otomatik birikim tanımlanmamış.";
                    }
                }

                GoalsList.ItemsSource = null; // Listeyi zorla yenile
                GoalsList.ItemsSource = goals;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }

        private string FormatTimeLeft(int totalDays)
        {
            if (totalDays < 30) return $"{totalDays} gün";
            int months = totalDays / 30;
            int days = totalDays % 30;
            return days == 0 ? $"{months} ay" : $"{months} ay {days} gün";
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
                                             $"({goal.CurrentAmount:N2} ₺ bakiyenize iade edilecek.)",
                    "Silme ve İade Onayı",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 1. İçerideki parayı ana bakiyeye iade et
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

                        // --- 2. YENİ EKLENEN KISIM: Bağlı Otomasyonları Sil ---
                        var linkedAutomations = _userRepository.GetGoalAutomations(_currentUserId)
                                                               .Where(a => a.GoalId == goal.GoalId).ToList();
                        foreach (var auto in linkedAutomations)
                        {
                            _userRepository.DeleteGoalAutomation(auto.AutomationId);
                        }
                        // --------------------------------------------------------

                        // 3. Son olarak hedefin kendisini sil
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

                            MessageBox.Show($"{amount:N0} ₺ hedefe eklendi 🎯");
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
        private void AutoSave_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Goal goal)
            {
                // YENİ PENCEREYİ ÇAĞIR
                var dialog = new Windows.AutoSaveWindow();

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        // Önce bu hedefe ait var olan eski otomasyonları temizle
                        var existingAutos = _userRepository.GetGoalAutomations(_currentUserId)
                                                           .Where(a => a.GoalId == goal.GoalId).ToList();
                        foreach (var oldAuto in existingAutos)
                        {
                            _userRepository.DeleteGoalAutomation(oldAuto.AutomationId);
                        }

                        // Eğer "SİL/İPTAL" butonuna basıldıysa sadece silmiş olduk, yeni kayıt atma.
                        if (dialog.IsCancelled)
                        {
                            MessageBox.Show("Otomatik birikim iptal edildi.");
                        }
                        else
                        {
                            // "KAYDET"e basıldıysa yeni otomasyonu oluştur
                            var newAuto = new GoalAutomation
                            {
                                UserId = _currentUserId,
                                GoalId = goal.GoalId,
                                Amount = dialog.Amount,
                                Frequency = dialog.Frequency,
                                NextRunDate = CalculateNextRunDate(DateTime.Now, dialog.Frequency),
                                IsActive = true
                            };

                            _userRepository.AddGoalAutomation(newAuto);

                            // Frekans metnini Türkçeleştirip gösterelim
                            string freqText = dialog.Frequency == "Daily" ? "her gün" : dialog.Frequency == "Weekly" ? "her hafta" : "her ay";
                            MessageBox.Show($"Başarılı! Bu hedefe {freqText} {dialog.Amount:N2} ₺ eklenecek. 🔄");
                        }

                        // UI'ı ve Tahminleme metnini yenile
                        LoadGoals();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"İşlem hatası: {ex.Message}");
                    }
                }
            }
        }

        // Bu metot eğer dosyanda yoksa AutoSave_Click'in altına ekle
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