using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Kumparam.Data.Repositories;
using Kumparam.UI.Services;
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

            // Varsayılan olarak "Bu Ay" seçili gelsin
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

        private void SetDateRange(string rangeType)
        {
            DateTime now = DateTime.Now;

            switch (rangeType)
            {
                case "Today":
                    StartDatePicker.SelectedDate = now.Date;
                    EndDatePicker.SelectedDate = now.Date; // Gün sonunu filtrede halledeceğiz
                    break;
                case "Week":
                    // Haftanın başı (Pazartesi)
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
                    StartDatePicker.SelectedDate = null; // Null "Başlangıçtan beri" demek olsun
                    EndDatePicker.SelectedDate = null;
                    break;
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Tarihleri Kontrol Et
                DateTime? start = StartDatePicker.SelectedDate;
                DateTime? end = EndDatePicker.SelectedDate;

                if (start.HasValue && end.HasValue && start > end)
                {
                    MessageBox.Show("Başlangıç tarihi bitiş tarihinden büyük olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Verileri Çek ve Filtrele
                var allTransactions = _userRepository.GetAllTransactions(_currentUserId);
                
                // Filtreleme (LINQ ile)
                var filteredTransactions = allTransactions.Where(t => 
                    (!start.HasValue || t.TransactionDate.Date >= start.Value.Date) &&
                    (!end.HasValue || t.TransactionDate.Date <= end.Value.Date)
                ).ToList();

                if (filteredTransactions.Count == 0)
                {
                    MessageBox.Show("Seçilen tarih aralığında hiç işlem bulunamadı.", "Veri Yok", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 3. Dosya Kaydet ve PDF Oluştur
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"Kumparam_Rapor_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    Title = "Raporu Kaydet"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var userProfile = _userRepository.GetUserProfile(_currentUserId);
                    var pdfService = new PdfReportService();
                    
                    pdfService.GeneratePdf(saveFileDialog.FileName, userProfile, filteredTransactions);

                    MessageBox.Show("Rapor başarıyla oluşturuldu! 📄", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // İşlem bitince pencereyi kapat
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