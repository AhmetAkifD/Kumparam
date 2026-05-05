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
using System.Xml.Linq;

namespace Kumparam.Pages.DashboardSubPages;

public partial class AdminView : UserControl
{
    private readonly IUserRepository _userRepository;
    private ScrapingConfig? _selectedConfig = null; // Düzenlenen kayıt
    
    // 1. DÖVİZ (doviz.com) Template'leri
    private const string Tpl_Url_Currency = "https://kur.doviz.com/"; 
    private const string Tpl_XP_Currency_Buy = "//td[@data-socket-key=\"\" and @data-socket-attr=\"bid\"]";
    private const string Tpl_XP_Currency_Sell = "//td[@data-socket-key=\"\" and @data-socket-attr=\"ask\"]";

    // 2. ALTIN (altin.doviz.com) Template'leri
    private const string Tpl_Url_Gold = "https://altin.doviz.com/";
    private const string Tpl_XP_Gold_Buy = "//td[@data-socket-key=\"\" and @data-socket-attr=\"bid\"]";
    private const string Tpl_XP_Gold_Sell = "//td[@data-socket-key=\"\" and @data-socket-attr=\"ask\"]";

    // 3. BORSA (borsa.doviz.com) Template'leri
    private const string Tpl_Url_Stock = "https://borsa.doviz.com/hisseler/";
    private const string Tpl_XP_Stock_Last = "//tr[@id=\"\"]/td[@class=\"text-bold\"]\n";

    // 4. KRİPTO (doviz.com/kripto) Template'leri
    private const string Tpl_Url_Crypto = "https://www.doviz.com/kripto-paralar";
    private const string Tpl_XP_Crypto_Last = "//tr[td//div[text()=\"\"]]/td[3]";
    
    // 5. TEFAS Template'leri
    private const string Tpl_Url_Fund = "https://www.tefas.gov.tr/FonAnaliz.aspx?FonKod=[LINK-ADI]";
    private const string Tpl_XP_Fund_Price = "//div[@class='main-indicators']/ul[@class='top-list']/li[1]/span";

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
        SourceTypeComboBox.SelectedIndex = 0; 
        UrlTextBox.Clear();
        UrlTextBox.IsEnabled = true;
        XPathSellingBox.Clear();
        XPathSellingBox.IsEnabled = true;
        XPathBuyingBox.Clear();
        XPathBuyingBox.IsEnabled = true;
    
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
            // Kaynağa göre ComboBox'ı seç
            if (config.SourceType == "TCMB")
                SourceTypeComboBox.SelectedIndex = 1; // TCMB
            else 
                SourceTypeComboBox.SelectedIndex = 0; // Web
            UrlTextBox.Text = config.TargetUrl;
            XPathSellingBox.Text = config.HtmlPath_Selling; // YENİ
            XPathBuyingBox.Text = config.HtmlPath_Buying;   // YENİ
            ActiveCheckBox.IsChecked = config.IsActive;
            
            TestResultText.Text = "Kayıt seçildi.";
        }
    }

    private async void TestConfig_Click(object sender, RoutedEventArgs e)
    {
        // 1. BOŞ KONTROLÜ (Validation)
        if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
        {
            MessageBox.Show("Lütfen bir URL giriniz.", "Eksik Bilgi");
            return;
        }

        if (string.IsNullOrWhiteSpace(XPathSellingBox.Text) && string.IsNullOrWhiteSpace(XPathBuyingBox.Text))
        {
            MessageBox.Show("En az bir XPath (Satış veya Alış) girmelisiniz.", "Eksik Bilgi");
            return;
        }

        // Hissenin kapalı kutusu için kontrol yapmaya gerek yok, açık olanlara bakacağız.

        TestResultText.Text = "Bağlanılıyor...";
        TestResultText.Foreground = System.Windows.Media.Brushes.Orange;

        try
        {
            using (var client = new HttpClient())
            {
                // Tarayıcı Taklidi (User-Agent)
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
                
                var html = await client.GetStringAsync(UrlTextBox.Text);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                string resultMsg = "";
                bool anySuccess = false;

                // Satış (veya Hisse Son Fiyat) Testi
                if (!string.IsNullOrWhiteSpace(XPathSellingBox.Text))
                {
                    var node = doc.DocumentNode.SelectSingleNode(XPathSellingBox.Text);
                    if (node != null)
                    {
                        // Hisse senedi ise "Son Fiyat:", değilse "Satış:" yazalım
                        bool isStock = XPathBuyingBox.IsEnabled == false; 
                        string label = isStock ? "Son Fiyat" : "Satış";
                        
                        resultMsg += $"{label}: {node.InnerText.Trim()} ";
                        anySuccess = true;
                    }
                    else resultMsg += "Veri 1: ❌ ";
                }

                // Alış Testi (Sadece kutu aktifse test et)
                if (XPathBuyingBox.IsEnabled && !string.IsNullOrWhiteSpace(XPathBuyingBox.Text))
                {
                    var node = doc.DocumentNode.SelectSingleNode(XPathBuyingBox.Text);
                    if (node != null)
                    {
                        resultMsg += $"| Alış: {node.InnerText.Trim()}";
                        anySuccess = true;
                    }
                    else resultMsg += "| Alış: ❌";
                }

                TestResultText.Text = resultMsg;
                TestResultText.Foreground = anySuccess ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
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
            // En üstte tanımladığını varsaydığım BankSourceComboBox'tan veriyi al
            string dbSourceType = "Web";
            if (BankSourceComboBox.SelectedItem is ComboBoxItem item)
            {
                dbSourceType = item.Tag?.ToString() ?? "Web";
            }

            var config = new ScrapingConfig
            {
                Symbol = SymbolTextBox.Text.Trim(),
                Description = DescTextBox.Text,
                TargetUrl = UrlTextBox.Text,
                HtmlPath_Selling = XPathSellingBox.Text, 
                HtmlPath_Buying = XPathBuyingBox.Text,   
                SourceType = dbSourceType, 
                IsActive = ActiveCheckBox.IsChecked == true
            };

            if (_selectedConfig == null)
            {
                _userRepository.AddScrapingConfig(config);
                MessageBox.Show("Yeni ayar eklendi. ✅");
            }
            else
            {
                config.ConfigId = _selectedConfig.ConfigId; // Bunu unutmamak lazım
                _userRepository.UpdateScrapingConfig(config);
                MessageBox.Show("Ayar güncellendi. ✅");
            }

            LoadConfigs();
            NewConfig_Click(null, null); 
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
    private void SourceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceTypeComboBox.SelectedItem is ComboBoxItem selectedItem && UrlTextBox != null)
        {
            string tag = selectedItem.Tag.ToString()!;

            // Varsayılan: Her şey açık ve boş
            UrlTextBox.IsEnabled = true;
            XPathSellingBox.IsEnabled = true;
            XPathBuyingBox.IsEnabled = true; 
            
            // Hisse senedinde "Alış" kutusu (ikinci kutu) kapalı olacak, çünkü tek fiyat var.
            // Biz "Satış" kutusunu (SellingBox) ana fiyat kutusu olarak kullanacağız.

            switch (tag)
            {
                case "Web_Currency":
                    UrlTextBox.Text = Tpl_Url_Currency;
                    XPathBuyingBox.Text = Tpl_XP_Currency_Buy;
                    XPathSellingBox.Text = Tpl_XP_Currency_Sell;
                    DescTextBox.Text = "";
                    break;

                case "Web_Gold":
                    UrlTextBox.Text = Tpl_Url_Gold;
                    XPathBuyingBox.Text = Tpl_XP_Gold_Buy;
                    XPathSellingBox.Text = Tpl_XP_Gold_Sell;
                    DescTextBox.Text = "";
                    break;

                case "Web_Stock":
                    UrlTextBox.Text = Tpl_Url_Stock;
                    
                    // Hisse için tek fiyat yeterli, onu da 'Selling' kutusuna yazıyoruz (Ana fiyat)
                    XPathSellingBox.Text = Tpl_XP_Stock_Last;
                    
                    // Alış kutusunu kapat ve temizle
                    XPathBuyingBox.Text = ""; 
                    XPathBuyingBox.IsEnabled = false; 
                    DescTextBox.Text = "";
                    break;

                case "Web_Crypto":
                    UrlTextBox.Text = Tpl_Url_Crypto;
                    XPathSellingBox.Text = Tpl_XP_Crypto_Last;
                    DescTextBox.Text = "";
                    XPathBuyingBox.IsEnabled = false;
                    break;
                case "Web_Fund":
                    UrlTextBox.Text = Tpl_Url_Fund;
    
                    // Fon için de tek fiyat (Son Fiyat) geçerlidir
                    XPathSellingBox.Text = Tpl_XP_Fund_Price;
    
                    // Alış kutusunu KAPATIYORUZ
                    XPathBuyingBox.Text = "";
                    XPathBuyingBox.IsEnabled = false;
    
                    DescTextBox.Text = "Yatırım Fonu";
                    break;

                default: // Custom
                    UrlTextBox.Clear();
                    XPathBuyingBox.Clear();
                    XPathSellingBox.Clear();
                    DescTextBox.Clear();
                    break;
            }
        }
    }
    private void BankSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // KALKAN: Sayfa yüklenmeden arayüz objeleri null iken çalışmasını engeller
        if (!this.IsLoaded || SourceTypeComboBox == null || UrlTextBox == null) return;

        if (BankSourceComboBox.SelectedItem is ComboBoxItem item)
        {
            string tag = item.Tag?.ToString() ?? "Web";

            if (tag == "ZiraatBankasi")
            {
                // Ziraat şablonunu otomatik doldur ve alttaki detaylı ComboBox'ı gizle
                SourceTypeComboBox.Visibility = Visibility.Collapsed;
            
                UrlTextBox.Text = "https://www.ziraatbank.com.tr/tr/fiyatlar-ve-oranlar";
                XPathBuyingBox.Text = "//td[contains(text(), '[CODE]')]/following-sibling::td[1]";
                XPathSellingBox.Text = "//td[contains(text(), '[CODE]')]/following-sibling::td[2]";
            
                if (string.IsNullOrWhiteSpace(DescTextBox.Text)) 
                    DescTextBox.Text = "Ziraat Bankası - ";
            
                UrlTextBox.IsEnabled = true;
                XPathBuyingBox.IsEnabled = true;
                XPathSellingBox.IsEnabled = true;
            }
            else if (tag == "Halkbank")
            {
                SourceTypeComboBox.Visibility = Visibility.Collapsed;
    
                // Halkbank'ın kendi döviz/altın sayfası URL'si
                UrlTextBox.Text = "https://www.halkbank.com.tr/tr/piyasalar"; 
    
                // Halkbank'ın tablosuna uygun XPath şablonları (Ziraat'ten farklı olabilir, incelemek lazım)
                XPathBuyingBox.Text = "//td[contains(text(), '[CODE]')]/following-sibling::td[1]"; 
                XPathSellingBox.Text = "//td[contains(text(), '[CODE]')]/following-sibling::td[2]";
    
                if (string.IsNullOrWhiteSpace(DescTextBox.Text)) 
                    DescTextBox.Text = "Halkbank - ";
        
                UrlTextBox.IsEnabled = true;
                XPathBuyingBox.IsEnabled = true;
                XPathSellingBox.IsEnabled = true;
            }
            else if (tag == "YapiKrediBankasi")
            {
                SourceTypeComboBox.Visibility = Visibility.Collapsed;
    
                // Halkbank'ın kendi döviz/altın sayfası URL'si
                UrlTextBox.Text = "https://www.yapikredi.com.tr/yatirimci-kosesi/doviz-bilgileri"; 
    
                // Halkbank'ın tablosuna uygun XPath şablonları (Ziraat'ten farklı olabilir, incelemek lazım)
                XPathBuyingBox.Text = "//*[@id=\"currencyResultContent\"]/tr[satır]/td[3]"; 
                XPathSellingBox.Text = "//*[@id=\"currencyResultContent\"]/tr[satır]/td[4]";
    
                if (string.IsNullOrWhiteSpace(DescTextBox.Text)) 
                    DescTextBox.Text = "Yapı Kredi Bankası - ";
        
                UrlTextBox.IsEnabled = true;
                XPathBuyingBox.IsEnabled = true;
                XPathSellingBox.IsEnabled = true;
            }
            else
            {
                // Diğer web siteleri (Web) seçilirse alt şablonları tekrar göster
                SourceTypeComboBox.Visibility = Visibility.Visible;
            
                // Eğer yeni ekleme modundaysak alanları temizle ki eski Ziraat verileri kalmasın
                if (_selectedConfig == null)
                {
                    SourceTypeComboBox.SelectedIndex = 5; // Özel/Diğer
                    UrlTextBox.Clear();
                    XPathBuyingBox.Clear();
                    XPathSellingBox.Clear();
                }
            }
        }
    }
}