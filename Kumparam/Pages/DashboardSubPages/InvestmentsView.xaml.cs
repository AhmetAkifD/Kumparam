using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using Kumparam.Core; // LINQ (FirstOrDefault) için gerekli
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using Kumparam.Data.Services;

namespace Kumparam.Pages.DashboardSubPages;

public class InvestmentOption
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; 
}

public partial class InvestmentsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    private readonly IFinancialDataService _scrapingService;
    private List<InvestmentOption> _supportedInvestments = new List<InvestmentOption>();

    public InvestmentsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;
        
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        
        _userRepository = new SqlUserRepository(connectionString);
        _scrapingService = new WebScrapingService(_userRepository);
        
        InvestmentComboBox.ItemsSource = _supportedInvestments;
        InvestmentComboBox.DisplayMemberPath = "Name"; 
        InvestmentComboBox.SelectedValuePath = "Symbol";
        PurchaseDatePicker.SelectedDate = DateTime.Now;
        LoadDynamicOptions();
        _ = LoadInvestmentsAsync();
    }
    private void LoadDynamicOptions()
    {
        try
        {
            _supportedInvestments.Clear(); // Listeyi temizle

            // Veritabanındaki HER ŞEYİ çek (USD, EUR ve Altın hepsi burada artık)
            var allConfigs = _userRepository.GetAllScrapingConfigs();

            foreach (var config in allConfigs)
            {
                if (config.IsActive)
                {
                    _supportedInvestments.Add(new InvestmentOption
                    {
                        Name = config.Description ?? config.Symbol,
                        Symbol = config.Symbol,
                        SourceType = config.SourceType // "TCMB" veya "Web"
                    });
                }
            }
            
            // ComboBox'ı yenile
            InvestmentComboBox.ItemsSource = null;
            InvestmentComboBox.ItemsSource = _supportedInvestments;
            InvestmentComboBox.DisplayMemberPath = "Name"; 
            InvestmentComboBox.SelectedValuePath = "Symbol";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Liste yüklenirken hata: " + ex.Message);
        }
    }

    public InvestmentsView()
    {
        InitializeComponent();
    }

    // YARDIMCI METOT: Hangi servisi kullanacağına karar verir
    private async Task<decimal> GetSmartPriceAsync(string symbol, bool isBuyingFromBank = false)
    {
        // 1. Seçilen sembolün kaynağını bul
        var option = _supportedInvestments.FirstOrDefault(x => x.Symbol == symbol);
        
        // Eğer listede yoksa veya veritabanında silindiyse 0 dön
        if (option == null) return 0;
        // Web Servisine git
        if (isBuyingFromBank)
            return await _scrapingService.GetBuyingPriceAsync(symbol);
        else
            return await _scrapingService.GetPriceAsync(symbol);
    }

    private async Task LoadInvestmentsAsync()
    {
        try
        {
            var investments = _userRepository.GetInvestments(_currentUserId);

            foreach (var investment in investments)
            {
                if (!string.IsNullOrEmpty(investment.Symbol))
                {
                    // YENİ: Akıllı fiyat çekiciyi kullan
                    decimal livePrice = await GetSmartPriceAsync(investment.Symbol);
                    
                    if (livePrice > 0)
                    {
                        investment.CurrentPrice = livePrice;
                    }
                }
            }

            InvestmentsList.ItemsSource = null;
            InvestmentsList.ItemsSource = investments;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yatırımlar yüklenirken hata: {ex.Message}");
        }
    }

    private void InvestmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InvestmentComboBox.SelectedItem is InvestmentOption selectedOption)
        {
            SymbolTextBox.Text = selectedOption.Symbol;
        }
    }

    private async void SaveInvestment_Click(object sender, RoutedEventArgs e)
    {
        // 1. Temel Validasyonlar (Boş mu, sayı mı?)
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

            // 2. Fiyatı Öğren (İnternetten Çek)
            decimal costPrice = 0;
            if (!string.IsNullOrEmpty(selectedOption.Symbol))
            {
                costPrice = await GetSmartPriceAsync(selectedOption.Symbol);
            }

            // Fiyat çekilemediyse manuel sor
            if (costPrice == 0)
            {
                var inputDialog = new SimpleInputWindow("Fiyat çekilemedi. Alış Fiyatını Girin:");
                if (inputDialog.ShowDialog() == true)
                {
                    if (!decimal.TryParse(inputDialog.ResultText, out costPrice) || costPrice <= 0)
                    {
                        MessageBox.Show("Geçersiz fiyat. İşlem iptal.");
                        return;
                    }
                }
                else
                {
                    return; // İptal edildi
                }
            }

            // 3. BAKİYE KONTROLÜ (İşte Burası!) 🛑
            // İşlem ne kadar tutacak?
            decimal totalAmount = quantity * costPrice;

            // Cebimizde ne kadar var?
            var summary = _userRepository.GetFinancialSummary(_currentUserId);
            decimal currentBalance = summary.TotalBalance;

            if (totalAmount > currentBalance)
            {
                MessageBox.Show($"Yetersiz Bakiye!\n\n" +
                                $"Mevcut Bakiye: {currentBalance:N2} ₺\n" +
                                $"İşlem Tutarı: {totalAmount:N2} ₺\n" +
                                $"Eksik: {totalAmount - currentBalance:N2} ₺", 
                                "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // <--- İşlemi burada kesiyoruz, veritabanına gitmiyor.
            }

            // 4. Her Şey Yolundaysa Kaydet (Varlık Ekle)
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

            // 5. Gider Fişi Kes (Bakiyeden Düş)
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

            _ = LoadInvestmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}");
        }
    }
    
    private void DeleteInvestment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid investmentId)
        {
            var result = MessageBox.Show("Bu yatırımı silmek istediğinize emin misiniz?", 
                                         "Silme Onayı", 
                                         MessageBoxButton.YesNo, 
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _userRepository.DeleteInvestment(investmentId);
                    _ = LoadInvestmentsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Silme hatası: {ex.Message}");
                }
            }
        }
    }

    private async void SellInvestment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Investment investment)
        {
            var inputDialog = new SimpleInputWindow("Satılacak Miktarı Giriniz:");
            if (inputDialog.ShowDialog() == true)
            {
                if (decimal.TryParse(inputDialog.ResultText, out decimal sellAmount) && sellAmount > 0)
                {
                    if (sellAmount > investment.Quantity)
                    {
                        MessageBox.Show("Sahip olduğunuzdan fazlasını satamazsınız!");
                        return;
                    }

                    try
                    {
                        // YENİ: Akıllı fiyat çekiciyi kullan (Satış için - Bankadan Alış Kuru)
                        decimal currentRate = await GetSmartPriceAsync(investment.Symbol, isBuyingFromBank: true);
                        
                        if (currentRate == 0) 
                        {
                            MessageBox.Show("Güncel satış kuru çekilemedi.");
                            return; 
                        }

                        decimal incomeTotal = sellAmount * currentRate;

                        decimal newQuantity = investment.Quantity - sellAmount;
                        if (newQuantity == 0)
                        {
                            _userRepository.DeleteInvestment(investment.InvestmentId);
                        }
                        else
                        {
                            _userRepository.UpdateInvestmentQuantity(investment.InvestmentId, newQuantity);
                        }

                        var newTransaction = new Transaction
                        {
                            UserId = _currentUserId,
                            Amount = incomeTotal,
                            Type = "Income",
                            Category = "Yatırım Satışı",
                            Description = $"{investment.Name} Satışı ({sellAmount:N2} Adet x {currentRate:N4})",
                            TransactionDate = DateTime.Now
                        };
                        _userRepository.AddTransaction(newTransaction);

                        MessageBox.Show($"Satış Başarılı! {incomeTotal:N2} ₺ hesabınıza eklendi.");
                        _ = LoadInvestmentsAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Satış hatası: {ex.Message}");
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