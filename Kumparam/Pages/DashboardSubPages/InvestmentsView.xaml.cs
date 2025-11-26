using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Data;
using System.Threading.Tasks;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
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
        new InvestmentOption { Name = "Gram Altın", Symbol = "GLD", Type = "Altın" },
        new InvestmentOption { Name = "Çeyrek Altın", Symbol = "QGLD", Type = "Altın" },
        new InvestmentOption { Name = "Amerikan Doları", Symbol = "USD", Type = "Döviz" },
        new InvestmentOption { Name = "Euro", Symbol = "EUR", Type = "Döviz" },
        new InvestmentOption { Name = "Sterlin", Symbol = "GBP", Type = "Döviz" },
        new InvestmentOption { Name = "BIST 100 (Hisse)", Symbol = "XU100", Type = "Borsa" },
        new InvestmentOption { Name = "Bitcoin", Symbol = "BTC", Type = "Kripto" },
        new InvestmentOption { Name = "Ethereum", Symbol = "ETH", Type = "Kripto" }
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

        LoadInvestmentsAsync();
        PurchaseDatePicker.SelectedDate = DateTime.Now;
    }

    public InvestmentsView()
    {
        InitializeComponent();
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
                    decimal livePrice = await _priceService.GetPriceAsync(investment.Symbol);
                    
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

    private void SaveInvestment_Click(object sender, RoutedEventArgs e)
    {
        if (InvestmentComboBox.SelectedItem == null)
        {
            MessageBox.Show("Lütfen listeden bir yatırım aracı seçin.");
            return;
        }

        if (!decimal.TryParse(QuantityTextBox.Text, out decimal quantity) || quantity <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir miktar girin.");
            return;
        }

        if (!decimal.TryParse(BuyingPriceTextBox.Text, out decimal buyingPrice) || buyingPrice <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir alış fiyatı girin.");
            return;
        }

        try
        {
            var selectedOption = (InvestmentOption)InvestmentComboBox.SelectedItem;

            var newInvestment = new Investment
            {
                UserId = _currentUserId,
                Name = selectedOption.Name,
                Symbol = selectedOption.Symbol,
                Quantity = quantity,
                BuyingPrice = buyingPrice,
                CurrentPrice = buyingPrice,
                PurchaseDate = PurchaseDatePicker.SelectedDate ?? DateTime.Now
            };

            _userRepository.AddInvestment(newInvestment);

            MessageBox.Show("Yatırım portföye eklendi! 📈");

            InvestmentComboBox.SelectedIndex = -1;
            SymbolTextBox.Clear();
            QuantityTextBox.Clear();
            BuyingPriceTextBox.Clear();
            PurchaseDatePicker.SelectedDate = DateTime.Now;

            LoadInvestmentsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}");
        }
    }
}