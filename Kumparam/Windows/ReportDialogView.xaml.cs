using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Microsoft.Win32;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Kumparam.Data.Repositories;
using Kumparam.UI.Services;
// GÜNCELLEME: Namespace düzeltildi
using Transaction = Kumparam.Core.Transaction; 

namespace Kumparam.Pages.DashboardSubPages
{
    public partial class ReportDialogView : UserControl
    {
        private readonly IUserRepository _userRepository;
        private readonly Guid _currentUserId;
        
        public ReportDialogView(Guid userId)
        {
            InitializeComponent();
            _currentUserId = userId;

            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);

            // Varsayılan
            SetDateRange("Month");
        }

        // Hızlı Seçim Mantığı
        private void QuickSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string tag)
            {
                SetDateRange(tag);
            }
        }

        // Manuel tarih değişirse etiketi "Özel" yap
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kullanıcı elle değiştirirse "Özel Aralık" moduna geçer
            // (Bu event, kodla değiştirince de tetiklenir, o yüzden basit bırakıyoruz)
            // İstersen burada _selectedRangeLabel = "Ozel_Aralik" yapabilirsin ama
            // QuickSelect sonrası da tetikleneceği için mantığı karıştırmamak adına ellemiyoruz.
        }

        private void SetDateRange(string rangeType)
        {
            DateTime now = DateTime.Now;

            switch (rangeType)
            {
                case "Today":
                    StartDatePicker.SelectedDate = now.Date;
                    EndDatePicker.SelectedDate = now.Date; 
                    break;
                case "Week":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    StartDatePicker.SelectedDate = now.AddDays(-1 * diff).Date;
                    EndDatePicker.SelectedDate = now.Date;
                    break;
                case "Month":
                    StartDatePicker.SelectedDate = new DateTime(now.Year, now.Month, 1);
                    EndDatePicker.SelectedDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                    break;
                case "Year":
                    StartDatePicker.SelectedDate = new DateTime(now.Year, 1, 1);
                    EndDatePicker.SelectedDate = new DateTime(now.Year, 12, 31);
                    break;
                case "All":
                    StartDatePicker.SelectedDate = null;
                    EndDatePicker.SelectedDate = null;
                    break;
            }
        }

private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime? start = StartDatePicker.SelectedDate;
                DateTime? end = EndDatePicker.SelectedDate;

                if (start.HasValue && end.HasValue && start > end)
                {
                    MessageBox.Show("Başlangıç tarihi bitiş tarihinden büyük olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. İŞLEMLERİ ÇEK (Mevcut)
                var allTransactions = _userRepository.GetAllTransactions(_currentUserId);
                
                var filteredTransactions = allTransactions.Where(t => 
                    (!start.HasValue || t.TransactionDate.Date >= start.Value.Date) &&
                    (!end.HasValue || t.TransactionDate.Date <= end.Value.Date)
                ).ToList();

                if (filteredTransactions.Count == 0)
                {
                    MessageBox.Show("Seçilen tarih aralığında hiç işlem bulunamadı.", "Veri Yok", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. YENİ: YATIRIMLARI ve HEDEFLERİ ÇEK (Hazırlık)
                // Bu verileri bir sonraki adımda PDF servisine göndereceğiz.
                List<Investment> investments = _userRepository.GetInvestments(_currentUserId);
                List<Goal> goals = _userRepository.GetGoals(_currentUserId);

                // Dosya İsmi Oluşturma
                string datePart = "";
                if (start.HasValue && end.HasValue)
                    datePart = $"{start.Value:yyyyMMdd}-{end.Value:yyyyMMdd}";
                else if (start.HasValue)
                    datePart = $"{start.Value:yyyyMMdd}_Sonrasi";
                else
                    datePart = "Tum_Zamanlar";

                string fileName = $"Kumparam_Rapor_{datePart}.pdf";

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
                    
                    // PDF Başlık Metni
                    string reportPeriodText = "Tarih Aralığı: ";
                    if (start.HasValue && end.HasValue)
                        reportPeriodText += $"{start.Value:dd.MM.yyyy} - {end.Value:dd.MM.yyyy}";
                    else if (start.HasValue)
                        reportPeriodText += $"{start.Value:dd.MM.yyyy} tarihinden itibaren";
                    else
                        reportPeriodText += "Tüm Zamanlar";

                    // NOT: Şimdilik eski metodu çağırıyoruz ki hata vermesin.
                    // Bir sonraki adımda investments ve goals listelerini de buraya ekleyeceğiz.
                    pdfService.GeneratePdf(saveFileDialog.FileName, userProfile, filteredTransactions, investments, goals, reportPeriodText);

                    MessageBox.Show("Rapor başarıyla oluşturuldu! 📄", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseWindow();
                }
            }
            catch (Exception ex)
            {
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