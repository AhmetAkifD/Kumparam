using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
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
    private readonly IFinancialDataService _priceService;

    private readonly List<InvestmentOption> _supportedInvestments = new List<InvestmentOption>
    {
        new InvestmentOption { Name = "Amerikan Doları", Symbol = "USD", Type = "Döviz" },
        new InvestmentOption { Name = "Euro", Symbol = "EUR", Type = "Döviz" }
    };

    public InvestmentsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;
        
        _priceService = new TcmbDataService();

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        InvestmentComboBox.ItemsSource = _supportedInvestments;
        InvestmentComboBox.DisplayMemberPath = "Name"; 
        InvestmentComboBox.SelectedValuePath = "Symbol";
        PurchaseDatePicker.SelectedDate = DateTime.Now;

        _ = LoadInvestmentsAsync();
    }

    public InvestmentsView()
    {
        InitializeComponent();
    }

    private async Task LoadInvestmentsAsync()
    {
        try
        {
            // 1. Veritabanından sadece maliyet bilgilerini çek
            var investments = _userRepository.GetInvestments(_currentUserId);

            // 2. RAM üzerindeki nesnelerin Fiyatını internetten güncelle
            foreach (var investment in investments)
            {
                if (!string.IsNullOrEmpty(investment.Symbol))
                {
                    // TCMB'ye sor: "USD ne kadar?"
                    decimal livePrice = await _priceService.GetPriceAsync(investment.Symbol);
                    
                    if (livePrice > 0)
                    {
                        // Veritabanına yazmıyoruz, sadece ekranda göstermek için nesneyi güncelliyoruz
                        investment.CurrentPrice = livePrice;
                    }
                }
            }

            // 3. Listeyi yenile (Kâr/Zarar otomatik hesaplanmış olacak)
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
        // Validasyonlar
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

            // 1. Güncel Kuru Çek (Maliyet Hesabı İçin)
            decimal costPrice = 0;
            if (!string.IsNullOrEmpty(selectedOption.Symbol))
            {
                costPrice = await _priceService.GetPriceAsync(selectedOption.Symbol);
            }

            if (costPrice == 0)
            {
                MessageBox.Show("Güncel kur çekilemedi. İnternet bağlantınızı kontrol edin.");
                return; 
            }

            // 2. Yatırımı Oluştur ve Kaydet (Varlık Ekleme)
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

            // --- YENİ KISIM BAŞLANGICI ---
            
            // 3. İşlem Kaydı Oluştur ve Kaydet (Bakiyeden Düşme - Gider)
            // Toplam Tutar = Miktar * Birim Fiyat
            decimal totalAmount = quantity * costPrice;

            var newTransaction = new Transaction
            {
                UserId = _currentUserId,
                Amount = totalAmount,
                Type = "Expense", // Gider olarak düşüyoruz
                Category = "Yatırım",
                Description = $"{selectedOption.Name} Alımı ({quantity:N2} Adet x {costPrice:N2})",
                TransactionDate = newInvestment.PurchaseDate
            };

            _userRepository.AddTransaction(newTransaction);

            // --- YENİ KISIM BİTİŞİ ---

            MessageBox.Show($"Yatırım Eklendi ve {totalAmount:N2} ₺ bakiyeden düşüldü!");

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
        // 1. Miktar Sor
        var inputDialog = new SimpleInputWindow();
        if (inputDialog.ShowDialog() == true)
        {
            if (decimal.TryParse(inputDialog.ResultText, out decimal sellAmount) && sellAmount > 0)
            {
                // 2. Miktar Kontrolü
                if (sellAmount > investment.Quantity)
                {
                    MessageBox.Show("Sahip olduğunuzdan fazlasını satamazsınız!");
                    return;
                }

                try
                {
                    // 3. Güncel Alış Kurunu Çek (Biz satıyoruz, banka alıyor -> BuyingRate)
                    decimal currentRate = await _priceService.GetBuyingPriceAsync(investment.Symbol);
                    
                    if (currentRate == 0) 
                    {
                        // Eğer kur çekilemezse varsayılan olarak o anki görünen fiyatı veya manuel girişi kullanabiliriz
                        // Şimdilik uyarı verelim
                        MessageBox.Show("Güncel satış kuru çekilemedi.");
                        return; 
                    }

                    // 4. Gelir Hesapla
                    decimal incomeTotal = sellAmount * currentRate;

                    // 5. Veritabanı Güncellemeleri
                    
                    // A) Miktarı Azalt veya Sil
                    decimal newQuantity = investment.Quantity - sellAmount;
                    if (newQuantity == 0)
                    {
                        _userRepository.DeleteInvestment(investment.InvestmentId);
                    }
                    else
                    {
                        _userRepository.UpdateInvestmentQuantity(investment.InvestmentId, newQuantity);
                    }

                    // B) Gelir Fişi Kes (Transaction - Income)
                    var newTransaction = new Transaction
                    {
                        UserId = _currentUserId,
                        Amount = incomeTotal,
                        Type = "Income", // Gelir olarak ekleniyor
                        Category = "Yatırım Satışı",
                        Description = $"{investment.Name} Satışı ({sellAmount:N2} Adet x {currentRate:N2})",
                        TransactionDate = DateTime.Now
                    };
                    _userRepository.AddTransaction(newTransaction);

                    MessageBox.Show($"Satış Başarılı! {incomeTotal:N2} ₺ hesabınıza eklendi.");
                    
                    // Listeyi Yenile
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