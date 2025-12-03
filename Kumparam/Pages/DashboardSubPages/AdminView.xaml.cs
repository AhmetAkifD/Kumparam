using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using HtmlAgilityPack; // Scraping için
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages;

public partial class AdminView : UserControl
{
    private readonly IUserRepository _userRepository;
    private ScrapingConfig? _selectedConfig = null; // Düzenlenen kayıt

    public AdminView()
    {
        InitializeComponent();
        
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        LoadConfigs();
    }

    private void LoadConfigs()
    {
        try
        {
            var configs = _userRepository.GetAllScrapingConfigs();
            ConfigsGrid.ItemsSource = configs;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ayarlar yüklenirken hata: " + ex.Message);
        }
    }

    private void NewConfig_Click(object sender, RoutedEventArgs e)
    {
        // Formu temizle, yeni kayıt moduna geç
        _selectedConfig = null;
        ConfigsGrid.SelectedItem = null;
        
        SymbolTextBox.Clear();
        DescTextBox.Clear();
        UrlTextBox.Clear();
        XPathTextBox.Clear();
        ActiveCheckBox.IsChecked = true;
        TestResultText.Text = "Yeni kayıt modu...";
        TestResultText.Foreground = System.Windows.Media.Brushes.Gray;
    }

    private void ConfigsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfigsGrid.SelectedItem is ScrapingConfig config)
        {
            _selectedConfig = config;
            
            // Formu doldur
            SymbolTextBox.Text = config.Symbol;
            DescTextBox.Text = config.Description;
            UrlTextBox.Text = config.TargetUrl;
            XPathTextBox.Text = config.HtmlPath;
            ActiveCheckBox.IsChecked = config.IsActive;
            
            TestResultText.Text = "Kayıt seçildi.";
        }
    }

    private async void TestConfig_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlTextBox.Text;
        string xpath = XPathTextBox.Text;

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(xpath))
        {
            MessageBox.Show("Lütfen URL ve XPath giriniz.");
            return;
        }

        TestResultText.Text = "Bağlanılıyor...";
        TestResultText.Foreground = System.Windows.Media.Brushes.Orange;

        try
        {
            // Anlık Scraping Testi (Service kullanmadan manuel deniyoruz ki DB'ye yazmadan görelim)
            using (var client = new HttpClient())
            {
                // Tarayıcı taklidi
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                var html = await client.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var node = doc.DocumentNode.SelectSingleNode(xpath);
                if (node != null)
                {
                    string rawText = node.InnerText.Trim();
                    TestResultText.Text = $"BAŞARILI! Okunan Veri: {rawText}";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    TestResultText.Text = "HATA: XPath ile veri bulunamadı.";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }
        catch (Exception ex)
        {
            TestResultText.Text = "HATA: " + ex.Message;
            TestResultText.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SymbolTextBox.Text))
        {
            MessageBox.Show("Sembol boş olamaz.");
            return;
        }

        try
        {
            var config = new ScrapingConfig
            {
                Symbol = SymbolTextBox.Text.ToUpper(),
                Description = DescTextBox.Text,
                TargetUrl = UrlTextBox.Text,
                HtmlPath = XPathTextBox.Text,
                IsActive = ActiveCheckBox.IsChecked == true
            };

            if (_selectedConfig == null)
            {
                // Yeni Ekle
                _userRepository.AddScrapingConfig(config);
                MessageBox.Show("Yeni ayar eklendi. ✅");
            }
            else
            {
                // Güncelle (Sembol değişmemeli veya ID ile kontrol edilmeli, burada basit update yapıyoruz)
                _userRepository.UpdateScrapingConfig(config);
                MessageBox.Show("Ayar güncellendi. ✅");
            }

            LoadConfigs();
            NewConfig_Click(null, null); // Formu temizle
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kaydetme hatası: " + ex.Message);
        }
    }

    private void DeleteConfig_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int configId)
        {
            if (MessageBox.Show("Bu ayarı silmek istediğinize emin misiniz?", "Sil", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _userRepository.DeleteScrapingConfig(configId);
                    LoadConfigs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme hatası: " + ex.Message);
                }
            }
        }
    }
}