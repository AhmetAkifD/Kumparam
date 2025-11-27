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
    public string Type { get; set; } = string.Empty;
}

public partial class InvestmentsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    
    // İki servisi de ayrı ayrı tutuyoruz
    private readonly IFinancialDataService _tcmbService;
    private readonly IFinancialDataService _scrapingService;

    // GENİŞLETİLMİŞ LİSTE
    private readonly List<InvestmentOption> _supportedInvestments = new List<InvestmentOption>
    {
        // Dövizler (TCMB)
        new InvestmentOption { Name = "Amerikan Doları", Symbol = "USD", Type = "Döviz" },
        new InvestmentOption { Name = "Euro", Symbol = "EUR", Type = "Döviz" },
        new InvestmentOption { Name = "İngiliz Sterlini", Symbol = "GBP", Type = "Döviz" },

        // Altınlar (Scraping)
        new InvestmentOption { Name = "Gram Altın", Symbol = "GLD", Type = "Altın" },
        new InvestmentOption { Name = "Çeyrek Altın", Symbol = "QGLD", Type = "Altın" },

        // Hisseler (Scraping - BIST)
        new InvestmentOption { Name = "THY (Hisse)", Symbol = "THYAO", Type = "Borsa" },
        new InvestmentOption { Name = "Aselsan (Hisse)", Symbol = "ASELS", Type = "Borsa" },
        new InvestmentOption { Name = "Garanti (Hisse)", Symbol = "GARAN", Type = "Borsa" },
        new InvestmentOption { Name = "Şişecam (Hisse)", Symbol = "SISE", Type = "Borsa" },
        new InvestmentOption { Name = "Koç Holding (Hisse)", Symbol = "KCHOL", Type = "Borsa" },

        // Kripto (Scraping)
        new InvestmentOption { Name = "Bitcoin", Symbol = "BTC", Type = "Kripto" },
        new InvestmentOption { Name = "Ethereum", Symbol = "ETH", Type = "Kripto" }
    };

    public InvestmentsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        // 1. İki servisi de başlat
        _tcmbService = new TcmbDataService();
        _scrapingService = new WebScrapingService();

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // 2. Arayüzü Doldur
        InvestmentComboBox.ItemsSource = _supportedInvestments;
        InvestmentComboBox.DisplayMemberPath = "Name"; 
        InvestmentComboBox.SelectedValuePath = "Symbol";
        PurchaseDatePicker.SelectedDate = DateTime.Now;

        // 3. Verileri Yükle
        _ = LoadInvestmentsAsync();
    }

    public InvestmentsView()
    {
        InitializeComponent();
    }

    // YARDIMCI METOT: Hangi servisi kullanacağına karar verir
    private async Task<decimal> GetSmartPriceAsync(string symbol, bool isBuyingFromBank = false)
    {
        // Listeden bu sembolün türünü bulalım (Döviz mi, Altın mı?)
        var option = _supportedInvestments.FirstOrDefault(x => x.Symbol == symbol);
        string type = option?.Type ?? "Diğer";

        IFinancialDataService activeService;

        // Eğer Döviz ise TCMB, değilse Scraping kullan
        if (type == "Döviz")
        {
            activeService = _tcmbService;
        }
        else
        {
            activeService = _scrapingService;
        }

        // Alış veya Satış fiyatını iste
        if (isBuyingFromBank)
            return await activeService.GetBuyingPriceAsync(symbol);
        else
            return await activeService.GetPriceAsync(symbol);
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

            // YENİ: Akıllı fiyat çekiciyi kullan (Maliyet için)
            decimal costPrice = 0;
            if (!string.IsNullOrEmpty(selectedOption.Symbol))
            {
                costPrice = await GetSmartPriceAsync(selectedOption.Symbol);
            }

            if (costPrice == 0)
            {
                // Eğer çekilemezse (internet yoksa veya borsa kapalıysa) uyar ama kayda izin ver
                // Şimdilik kullanıcıyı bilgilendirelim
                MessageBox.Show("Güncel fiyat çekilemedi, maliyet 0 olarak kaydedilecek. Daha sonra manuel düzenleyebilirsiniz.");
            }

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
            decimal totalAmount = quantity * costPrice;
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

            MessageBox.Show($"Yatırım Eklendi! (Maliyet Kuru: {costPrice:N4} ₺)");

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
            var inputDialog = new SimpleInputWindow();
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