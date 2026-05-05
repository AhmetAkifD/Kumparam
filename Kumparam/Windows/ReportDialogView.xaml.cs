using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Microsoft.Win32;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Kumparam.Data.Repositories;
using Kumparam.Data.Services;
using Kumparam.UI.Services;
using Transaction = Kumparam.Core.Transaction; 

namespace Kumparam.Pages.DashboardSubPages
{
    public partial class ReportDialogView : UserControl
    {
        private readonly IUserRepository _userRepository;
        private readonly Guid _currentUserId;
        
        private DateTime _startDate;
        private string _periodLabel = "Bu Ay";

        public ReportDialogView(Guid userId)
        {
            InitializeComponent();
            _currentUserId = userId;

            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);

            SetDateRange("Month");
        }

        private void QuickSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string tag)
            {
                SetDateRange(tag);
            }
        }

        private void SetDateRange(string rangeType)
        {
            DateTime now = DateTime.Now;
            _startDate = now;

            switch (rangeType)
            {
                case "Today":
                    _startDate = now.Date; 
                    _periodLabel = "Bugun";
                    break;
                case "Week":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    _startDate = now.AddDays(-1 * diff).Date;
                    _periodLabel = "Bu_Hafta";
                    break;
                case "Month":
                    _startDate = new DateTime(now.Year, now.Month, 1);
                    _periodLabel = "Bu_Ay";
                    break;
                case "Year":
                    _startDate = new DateTime(now.Year, 1, 1);
                    _periodLabel = "Bu_Yil";
                    break;
                case "All":
                    _startDate = DateTime.MinValue;
                    _periodLabel = "Tum_Zamanlar";
                    break;
            }

            if (rangeType == "All")
                TxtDatePreview.Text = "Başlangıçtan - Bugüne";
            else
                TxtDatePreview.Text = $"{_startDate:dd.MM.yyyy} - Bugün";
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(100);

            try
            {
                DateTime endDate = DateTime.Now;

                // 1. İŞLEMLERİ ÇEK VE FİLTRELE
                var allTransactions = await Task.Run(() => _userRepository.GetAllTransactions(_currentUserId));

                var filteredTransactions = allTransactions.Where(t =>
                    t.TransactionDate >= _startDate && t.TransactionDate <= endDate
                ).ToList();

                if (filteredTransactions.Count == 0)
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MessageBox.Show("Seçilen dönemde hiç işlem bulunamadı.", "Veri Yok", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. YATIRIMLARI ve HEDEFLERİ ÇEK
                List<Investment> investments = await Task.Run(() => _userRepository.GetInvestments(_currentUserId));
                List<Goal> goals = await Task.Run(() => _userRepository.GetGoals(_currentUserId));

                // 3. YATIRIM FİYATLARINI GÜNCELLEME
                try
                {
                    var scraper = new WebScrapingService(_userRepository);
                    foreach (var item in investments)
                    {
                        if (!string.IsNullOrEmpty(item.Symbol))
                        {
                            decimal price = await scraper.GetPriceAsync(item.Symbol);
                            if (price > 0) item.CurrentPrice = price;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Fiyat güncelleme hatası: " + ex.Message);
                }

                // 4. YENİ EKLENEN KISIM: YAPAY ZEKA ANALİZİ
                string aiAnalysisText = "";
                try
                {
                    var aiService = new AiAnalysisService();
                    aiAnalysisText = await aiService.GenerateFinancialAdviceAsync(filteredTransactions, investments, goals);
                }
                catch (Exception ex)
                {
                    aiAnalysisText = "Yapay zeka analizi şu anda oluşturulamıyor.";
                    System.Diagnostics.Debug.WriteLine("AI Hatası: " + ex.Message);
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;

                string fileName = $"Kumparam_Rapor_{_periodLabel}_{DateTime.Now:yyyyMMdd}.pdf";

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = fileName,
                    Title = "Raporu Kaydet"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var userProfile = _userRepository.GetUserProfile(_currentUserId);
                    var pdfService = new PdfReportService();

                    string reportPeriodText = _periodLabel == "Tum_Zamanlar"
                        ? "Tüm Zamanlar"
                        : $"Dönem: {_startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";

                    // DİKKAT: GeneratePdf metoduna aiAnalysisText parametresini de ekledik.
                    // (PdfReportService sınıfında da bu parametreyi karşılayacak küçük bir değişiklik yapacağız)
                    await Task.Run(() => pdfService.GeneratePdf(saveFileDialog.FileName, userProfile, filteredTransactions, investments, goals, reportPeriodText, aiAnalysisText));

                    MessageBox.Show("Rapor başarıyla oluşturuldu! 📄", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseWindow();
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void CloseWindow()
        {
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
    }
}