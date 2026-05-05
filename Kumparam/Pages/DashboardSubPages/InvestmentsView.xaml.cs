using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Kumparam.Core;
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using Kumparam.Data.Services;

namespace Kumparam.Pages.DashboardSubPages;

public partial class InvestmentsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    private readonly IFinancialDataService _scrapingService;

    // UI'ın dinlediği canlı liste (Gruplanmış)
    public ObservableCollection<PortfolioItem> PortfolioItems { get; set; }

    // ComboBox için seçenekler listesi
    public List<InvestmentOption> InvestmentOptions { get; set; }

    public InvestmentsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;
        TxtLastUpdate.Text = $"Son: {DateTime.Now:HH:mm}";
        
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);
        
        // Scraping servisini başlat (Parametre alıp almaması senin Service koduna bağlı, 
        // senin attığın kodda parametre vardı, onu korudum. Hata verirse parantez içini sil.)
        _scrapingService = new WebScrapingService(_userRepository); 
        // Not: Eğer senin WebScrapingService constructor'ında IUserRepository istiyorsa üstteki satırı silip şunu aç:
        // _scrapingService = new WebScrapingService(_userRepository);

        // 1. ComboBox Seçeneklerini Yükle
        LoadDynamicOptions("Web");

        // 2. Portföyü Yükle
        _ = LoadPortfolioAsync();
        
        // Tarihi bugüne ayarla
        PurchaseDatePicker.SelectedDate = DateTime.Now;
    }

    public InvestmentsView() { InitializeComponent(); }

    // --- 1. COMBOBOX DOLDURMA (Veritabanından) ---
    private void LoadDynamicOptions(string source)
    {
        // KALKAN GÜNCELLENDİ: Sadece veritabanı bağlantısı yoksa durdur. IsLoaded kontrolünü sildik.
        if (_userRepository == null) return; 

        try
        {
            var allConfigs = _userRepository.GetAllScrapingConfigs();
            if (allConfigs == null) return; 

            InvestmentOptions = new List<InvestmentOption>();

            foreach (var config in allConfigs)
            {
                if (config != null && config.IsActive && config.SourceType == source)
                {
                    InvestmentOptions.Add(new InvestmentOption
                    {
                        Symbol = config.Symbol,
                        Name = config.Description ?? config.Symbol ?? "Bilinmeyen",
                        SourceType = config.SourceType
                    });
                }
            }
        
            InvestmentComboBox.ItemsSource = InvestmentOptions;
            InvestmentComboBox.DisplayMemberPath = "Name"; 
            InvestmentComboBox.SelectedValuePath = "Symbol";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Liste yüklenirken hata: " + ex.Message);
        }
    }

    // --- 2. PORTFÖY YÜKLEME (GRUPLAMA MANTIĞI) ---
    private async Task LoadPortfolioAsync()
    {
        try
        {
            var rawInvestments = _userRepository.GetInvestments(_currentUserId);
    
            if (rawInvestments == null || !rawInvestments.Any()) 
            {
                PortfolioItems = new ObservableCollection<PortfolioItem>();
                PortfolioGrid.ItemsSource = PortfolioItems;
                CalculatePortfolioSummary();
                return;
            }

            var groupedList = rawInvestments
                .Where(x => x != null) 
                // YENİ: Ziraat ve Web'i ayrı satırlarda tutmak için
                .GroupBy(x => new { Symbol = x.Symbol ?? "Bilinmiyor", Source = x.Source ?? "Web" })
                .Select(g => new PortfolioItem
                {
                    Symbol = g.Key.Symbol,
                    SourceType = g.Key.Source,
                    Name = g.First().Name ?? g.Key.Symbol,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    AverageCost = g.Sum(x => x.Quantity) > 0 ? g.Sum(x => x.Quantity * x.BuyingPrice) / g.Sum(x => x.Quantity) : 0,
                    CurrentPrice = null
                })
                .ToList();

            PortfolioItems = new ObservableCollection<PortfolioItem>(groupedList);
            PortfolioGrid.ItemsSource = PortfolioItems;

            CalculatePortfolioSummary();
            await UpdatePricesAsync(); // Buradaki GetPriceAsync metoduna item.SourceType yollamayı unutma!
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Portföy yüklenirken hata: {ex.Message}");
        }
    }
    // --- 4. YENİ YATIRIM KAYDETME ---
    private async void SaveInvestment_Click(object sender, RoutedEventArgs e)
    {
        if (InvestmentComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(QuantityTextBox.Text))
        {
            MessageBox.Show("Lütfen yatırım türünü ve miktarını seçin.");
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, out decimal quantity) || quantity <= 0)
        {
            MessageBox.Show("Geçerli bir miktar girin.");
            return;
        }

        try
        {
            var selectedOption = (InvestmentOption)InvestmentComboBox.SelectedItem;

            // Fiyatı Çek (Alış Fiyatı - Banka Satışı)
            decimal costPrice = 0;
            if (!string.IsNullOrEmpty(selectedOption.Symbol))
            {
                costPrice = await _scrapingService.GetBuyingPriceAsync(selectedOption.Symbol);
            }

            // Fiyat çekilemediyse sor
            if (costPrice == 0)
            {
                var inputDialog = new SimpleInputWindow("Fiyat çekilemedi. Alış Fiyatını Girin:");
                if (inputDialog.ShowDialog() == true)
                {
                    if (!decimal.TryParse(inputDialog.ResultText, out costPrice) || costPrice <= 0) return;
                }
                else return;
            }

            // Bakiye Kontrolü
            decimal totalAmount = quantity * costPrice;
            var summary = _userRepository.GetFinancialSummary(_currentUserId);

            if (totalAmount > summary.TotalBalance)
            {
                MessageBox.Show($"Yetersiz Bakiye!\nGereken: {totalAmount:N2} ₺\nMevcut: {summary.TotalBalance:N2} ₺", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Yatırımı Kaydet
            var newInvestment = new Investment
            {
                UserId = _currentUserId,
                Name = selectedOption.Name,
                Symbol = selectedOption.Symbol,
                Quantity = quantity,
                BuyingPrice = costPrice, 
                PurchaseDate = PurchaseDatePicker.SelectedDate ?? DateTime.Now
            };
            _userRepository.AddInvestment(newInvestment);

            // Gider Fişi Kes
            var newTransaction = new Transaction
            {
                UserId = _currentUserId,
                Amount = totalAmount,
                Type = "Expense",
                Category = "Yatırım",
                Description = $"{selectedOption.Name} Alımı ({quantity:N2} Adet x {costPrice:N2})",
                TransactionDate = newInvestment.PurchaseDate
            };
            _userRepository.AddTransaction(newTransaction);

            MessageBox.Show($"Yatırım Eklendi! (Kur: {costPrice:N4} ₺)");

            // Temizlik
            InvestmentComboBox.SelectedIndex = -1;
            SymbolTextBox.Clear();
            QuantityTextBox.Clear();
            PurchaseDatePicker.SelectedDate = DateTime.Now;

            // Listeyi Yenile (Yeni Metot)
            await LoadPortfolioAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}");
        }
    }

    // --- 5. SATIŞ (CÜZDAN BOŞALTMA) ---
    private async void SellGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PortfolioItem item)
        {
            var inputDialog = new SimpleInputWindow($"{item.Symbol} Satış Miktarı:");
            if (inputDialog.ShowDialog() == true)
            {
                if (decimal.TryParse(inputDialog.ResultText, out decimal sellAmount) && sellAmount > 0)
                {
                    if (sellAmount > item.TotalQuantity)
                    {
                        MessageBox.Show("Elinizde olandan fazlasını satamazsınız!");
                        return;
                    }

                    // Satış Kuru (Banka Alışı)
                    // Fiyat henüz yüklenmediyse maliyetten satmaya çalışır (Fallback)
                    decimal currentRate = item.CurrentPrice ?? item.AverageCost;

                    // Eğer güncel fiyat yoksa tekrar çekmeyi dene
                    if (item.CurrentPrice == null)
                    {
                         currentRate = await _scrapingService.GetPriceAsync(item.Symbol);
                         if(currentRate == 0) currentRate = item.AverageCost;
                    }

                    // Gelir Ekle
                    var income = new Transaction
                    {
                        UserId = _currentUserId,
                        Amount = sellAmount * currentRate,
                        Type = "Income",
                        Category = "Yatırım",
                        Description = $"{item.Symbol} Satışı ({sellAmount:N2} Adet)",
                        TransactionDate = DateTime.Now
                    };
                    _userRepository.AddTransaction(income);

                    // Adet Düş
                    ReduceInvestmentQuantity(item.Symbol, sellAmount);

                    MessageBox.Show("Satış gerçekleşti ve bakiyenize eklendi! 💸");
                    await LoadPortfolioAsync();
                }
                else
                {
                    MessageBox.Show("Geçersiz miktar.");
                }
            }
        }
    }

    private void ReduceInvestmentQuantity(string symbol, decimal amountToSell)
    {
        var investments = _userRepository.GetInvestments(_currentUserId)
            .Where(x => x.Symbol == symbol)
            .OrderBy(x => x.PurchaseDate) // FIFO
            .ToList();

        decimal remainingToSell = amountToSell;

        foreach (var inv in investments)
        {
            if (remainingToSell <= 0) break;

            if (inv.Quantity <= remainingToSell)
            {
                _userRepository.DeleteInvestment(inv.InvestmentId);
                remainingToSell -= inv.Quantity;
            }
            else
            {
                decimal newQty = inv.Quantity - remainingToSell;
                _userRepository.UpdateInvestmentQuantity(inv.InvestmentId, newQty);
                remainingToSell = 0;
            }
        }
    }
    // InvestmentsView.xaml.cs içine ekle:

    private void OpenAddDialog_Click(object sender, RoutedEventArgs e)
    {
        // Burası için 2 seçenek var:
        // 1. Basit bir MessageBox ile "Admin panelinden veya başka yerden ekleyin" diyebiliriz.
        // 2. Veya şu anki sayfanın üzerine açılan bir Popup yapabiliriz.
    
        // Şimdilik basit tutalım:
        MessageBox.Show("Yeni yatırım ekleme ekranı bu tasarıma daha sonra entegre edilecek.\nŞimdilik veritabanından eklemeye devam edebilirsiniz.", "Bilgi");
    
        // NOT: Eğer eski sol paneldeki ekleme formunu kullanmak istiyorsan,
        // XAML'da sol tarafa bir "Expander" veya "Popup" koyup o formu oraya taşıyabiliriz.
        // İstersen bir sonraki adımda "Yeni Ekle" butonuna basınca açılan şık bir pencere yapalım.
    }
    private async void InvestmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InvestmentComboBox.SelectedItem is InvestmentOption selectedOption)
        {
            // 1. Sembolü yaz
            SymbolTextBox.Text = selectedOption.Symbol;

            // 2. Yüklenirken GRİ yap (Bu kalabilir, gri her iki temada da idare eder)
            CurrentPriceTextBox.Text = "Fiyat Getiriliyor...";
            CurrentPriceTextBox.SetResourceReference(Control.ForegroundProperty, "MaterialDesignBody");

            try
            {
                decimal price = await _scrapingService.GetBuyingPriceAsync(selectedOption.Symbol);

                if (price > 0)
                {
                    CurrentPriceTextBox.Text = $"{price:N2} ₺";
                    CurrentPriceTextBox.SetResourceReference(Control.ForegroundProperty, "MaterialDesignBody");
                }
                else
                {
                    CurrentPriceTextBox.Text = "Fiyat Alınamadı";
                    CurrentPriceTextBox.Foreground = System.Windows.Media.Brushes.Red; 
                }
            }
            catch (Exception ex)
            {
                CurrentPriceTextBox.Text = "Hata";
                CurrentPriceTextBox.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
    }
    // --- YENİ: SİLME (İPTAL) İŞLEMİ ---
    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PortfolioItem item)
        {
            // Kullanıcıya ne olacağını net anlatan bir uyarı
            var result = MessageBox.Show(
                $"DİKKAT: '{item.Name}' varlığını tamamen silmek üzeresiniz.\n\n" +
                $"Bu bir 'Satış' değildir.\n" +
                $"Bu varlık için harcanan toplam {item.TotalQuantity * item.AverageCost:N2} ₺ bakiyenize İADE edilecektir.\n\n" +
                "Onaylıyor musunuz?", 
                "Yatırım Silme", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. İade Edilecek Tutarı Hesapla (Toplam Maliyet)
                    decimal refundAmount = item.TotalQuantity * item.AverageCost;

                    // 2. O sembole ait tüm yatırımları veritabanından sil
                    var investmentsToDelete = _userRepository.GetInvestments(_currentUserId)
                                                             .Where(x => x.Symbol == item.Symbol)
                                                             .ToList();

                    foreach (var inv in investmentsToDelete)
                    {
                        _userRepository.DeleteInvestment(inv.InvestmentId);
                    }

                    // 3. Parayı Bakiyeye Geri Yükle (Gelir Fişi Kes)
                    if (refundAmount > 0)
                    {
                        var refundTransaction = new Transaction
                        {
                            UserId = _currentUserId,
                            Amount = refundAmount,
                            Type = "Income", // Gelir (Para geri dönüyor)
                            Category = "Yatırım İadesi",
                            Description = $"{item.Symbol} Yatırımı İptali/Silinmesi (İade)",
                            TransactionDate = DateTime.Now
                        };
                        _userRepository.AddTransaction(refundTransaction);
                    }

                    MessageBox.Show("Yatırım silindi ve tutar bakiyenize iade edildi.");
                    
                    // Listeyi Yenile
                    _ = LoadPortfolioAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Silme sırasında hata oluştu: " + ex.Message);
                }
            }
        }
    }
    // --- 1. YENİ METOT: ÖZET KARTLARINI HESAPLA ---
    private void CalculatePortfolioSummary()
    {
        // Eğer liste boşsa veya henüz oluşmadıysa işlem yapma
        if (PortfolioItems == null) return;

        decimal totalValue = 0; // Toplam Piyasa Değeri
        decimal totalCost = 0;  // Toplam Harcanan Para

        foreach (var item in PortfolioItems)
        {
            // Maliyet her zaman vardır, topla
            totalCost += item.TotalCost;
            
            // Eğer internetten fiyat geldiyse (IsPriceLoaded), güncel değeri topla.
            // Gelmediyse 0 kabul et (veya maliyeti ekle, ama 0 daha güvenli)
            if (item.IsPriceLoaded)
            {
                totalValue += item.CurrentValue;
            }
        }

        decimal totalProfit = totalValue - totalCost;

        // XAML'daki TextBlock'lara Yazdır
        // Not: Eğer totalValue henüz 0 ise (fiyatlar gelmediyse), kârı gösterme veya tire koy
        if (totalValue > 0)
        {
            HeaderTotalValueText.Text = $"{totalValue:N2} ₺";
            HeaderTotalProfitText.Text = $"{totalProfit:N2} ₺";
            
            // Renk Ayarı
            if (totalProfit >= 0)
                HeaderTotalProfitText.Foreground = System.Windows.Media.Brushes.Green;
            else
                HeaderTotalProfitText.Foreground = System.Windows.Media.Brushes.Red;
        }
        else
        {
            HeaderTotalValueText.Text = "Hesaplanıyor...";
            HeaderTotalProfitText.Text = "--- ₺";
            HeaderTotalProfitText.Foreground = System.Windows.Media.Brushes.Gray;
        }

        // Maliyet her zaman görünür
        HeaderTotalCostText.Text = $"{totalCost:N2} ₺";
    }

    // --- 2. GÜNCELLENMİŞ METOT: FİYATLAR GELDİKÇE HESAPLA ---
    private async Task UpdatePricesAsync()
    {
        // Tüm elemanlar için fiyatları çek
        foreach (var item in PortfolioItems)
        {
            decimal livePrice = await _scrapingService.GetPriceAsync(item.Symbol);
            
            if (livePrice > 0)
            {
                item.CurrentPrice = livePrice;
            }
            // DİKKAT: Buradaki "CalculatePortfolioSummary()" satırını sildik/kaldırdık.
            // Artık her adımda hesaplama yapmıyor.
        }

        // YENİ YERİ: Döngü bitti, tüm fiyatlar geldi. Şimdi TEK SEFERDE hesapla.
        CalculatePortfolioSummary();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        var dashboard = Window.GetWindow(this) as DashboardWindow;
        dashboard.MainContentArea.Content = new InvestmentsView(_currentUserId);
    }
    
    private void BankSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // KALKAN: Sayfa yüklenmeden arayüz objeleri null iken veya veritabanı yokken çalışmasını engeller
        if (!this.IsLoaded || _userRepository == null) return;

        if (BankSourceComboBox.SelectedItem is ComboBoxItem item)
        {
            // Seçilen öğenin Tag değerini (Web veya ZiraatBankasi) al
            string tag = item.Tag?.ToString() ?? "Web";

            // Yatırım araçları (InvestmentComboBox) listesini bu yeni kaynağa göre güncelle
            LoadDynamicOptions(tag);

            // Kaynak değiştiği için alttaki eski seçimleri ve kutuları temizle
            if (InvestmentComboBox != null) InvestmentComboBox.SelectedIndex = -1;
            if (SymbolTextBox != null) SymbolTextBox.Clear();
            // Eğer miktar veya güncel fiyat kutuların varsa onları da burada temizleyebilirsin:
            // if (CurrentPriceTextBox != null) CurrentPriceTextBox.Clear();
            // if (QuantityTextBox != null) QuantityTextBox.Clear();
        }
    }
}

// --- YARDIMCI SINIFLAR (Namespace İçine Taşındı - Dışarıda Olmalı) ---

public class InvestmentOption
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; 
}

public class PortfolioItem : INotifyPropertyChanged
{
    public string Symbol { get; set; }
    public string Name { get; set; }
    public string SourceType { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal AverageCost { get; set; } // Birim Maliyet (Hesaplama için lazım ama göstermeyeceğiz)
    
    // YENİ: Toplam Harcanan Para (Bu eksikti, o yüzden görünmüyordu)
    public decimal TotalCost => TotalQuantity * AverageCost; 

    private decimal? _currentPrice;
    public decimal? CurrentPrice
    {
        get => _currentPrice;
        set { 
            _currentPrice = value; 
            OnPropertyChanged(); 
            // Fiyat değişince tüm toplamları yeniden hesaplat
            OnPropertyChanged(nameof(CurrentValue)); 
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(ProfitLossAmount)); 
            OnPropertyChanged(nameof(ProfitLossPercent)); 
            OnPropertyChanged(nameof(IsPriceLoaded));
        }
    }

    public bool IsPriceLoaded => CurrentPrice.HasValue && CurrentPrice > 0;

    // Toplam Güncel Değer (Cüzdandaki Anlık Para)
    public decimal CurrentValue => (CurrentPrice ?? 0) * TotalQuantity;

    // Net Kâr/Zarar
    public decimal? ProfitLossAmount => IsPriceLoaded ? (CurrentValue - TotalCost) : null;

    // Yüzdelik
    public decimal? ProfitLossPercent
    {
        get
        {
            if (!IsPriceLoaded) return null;
            if (TotalCost == 0) return 0;
            return ((CurrentValue - TotalCost) / TotalCost) * 100;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

// Converterlar (XAML Erişimi İçin Buraya)
public class FirstCharConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s)) return s[0].ToString();
        return "?";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IsNegativeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return d < 0;
        return false;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}