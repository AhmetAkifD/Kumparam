using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Core.Models;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages;

public partial class TransactionsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    private List<Transaction> _allTransactions;

    public TransactionsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Tarih seçicileri hazırla
        TransactionDatePick.SelectedDate = DateTime.Now;
        StartDatePicker.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        EndDatePicker.SelectedDate = DateTime.Now;

        LoadData();
    }
    
    public TransactionsView() { InitializeComponent(); }

    private void LoadData()
    {
        try
        {
            _allTransactions = _userRepository.GetAllTransactions(_currentUserId);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Veri yükleme hatası: " + ex.Message);
        }
    }

    private void ApplyFilters()
    {
        if (_allTransactions == null) return;

        var query = _allTransactions.AsEnumerable();

        if (StartDatePicker.SelectedDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date >= StartDatePicker.SelectedDate.Value.Date);
            
        if (EndDatePicker.SelectedDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date <= EndDatePicker.SelectedDate.Value.Date);

        if (TypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            string type = selectedItem.Content.ToString();
            if (type == "Gelir") query = query.Where(t => t.Type == "Income");
            else if (type == "Gider") query = query.Where(t => t.Type == "Expense");
        }

        var filteredList = query.ToList();
        TransactionsGrid.ItemsSource = filteredList;

        decimal totalIncome = filteredList.Where(t => t.Type == "Income").Sum(t => t.Amount);
        decimal totalExpense = filteredList.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        decimal netBalance = totalIncome - totalExpense;

        TotalIncomeText.Text = $"{totalIncome:N2} ₺";
        TotalExpenseText.Text = $"{totalExpense:N2} ₺";
        NetBalanceText.Text = $"{netBalance:N2} ₺";
        NetBalanceText.Foreground = netBalance >= 0 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
    }

    // --- YENİ EKLENEN KISIM: KAYDETME ---
    private void SaveTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AmountBox.Text) || CategoryCombo.Text == "")
        {
            MessageBox.Show("Lütfen tutar ve kategori giriniz.");
            return;
        }

        if (!decimal.TryParse(AmountBox.Text, out decimal amount) || amount <= 0)
        {
            MessageBox.Show("Geçersiz tutar.");
            return;
        }

        try
        {
            // Seçilen türü (Gelir/Gider) belirle
            string type = "Expense"; // Varsayılan Gider
            if (NewTypeCombo.SelectedItem is ComboBoxItem item && item.Tag.ToString() == "Income")
            {
                type = "Income";
            }

            var newTransaction = new Transaction
            {
                UserId = _currentUserId,
                Amount = amount,
                Type = type,
                Category = CategoryCombo.Text,
                Description = DescriptionBox.Text,
                TransactionDate = TransactionDatePick.SelectedDate ?? DateTime.Now
            };

            _userRepository.AddTransaction(newTransaction);

            // Başarılı Mesajı
            MessageBox.Show("İşlem Kaydedildi! ✅");

            // Formu Temizle
            AmountBox.Clear();
            DescriptionBox.Clear();
            AmountBox.Focus();

            // LİSTEYİ YENİLE (En önemli kısım)
            LoadData(); 
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kayıt hatası: " + ex.Message);
        }
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void ClearFilters_Click(object sender, RoutedEventArgs e) 
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        TypeComboBox.SelectedIndex = 0;
        ApplyFilters();
    }

    private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid transactionId)
        {
            if (MessageBox.Show("Bu kaydı silmek istediğinize emin misiniz?", "Sil", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _userRepository.DeleteTransaction(transactionId);
                LoadData();
            }
        }
    }
}