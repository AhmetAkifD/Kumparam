using System;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages;

public partial class IncomeExpenseView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    public IncomeExpenseView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Tarih kutusunu bugüne ayarla
        TransactionDatePicker.SelectedDate = DateTime.Now;
        LoadTransactions();
    }

    // XAML Tasarımcısı için boş constructor (Hata almamak için)
    public IncomeExpenseView()
    {
        InitializeComponent();
    }
    
    private void DescriptionTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveButton_Click(sender, e);
        }
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. Validasyon (Boş alan kontrolü)
        if (TypeComboBox.SelectedItem == null)
        {
            MessageBox.Show("Lütfen işlem tipini (Gelir/Gider) seçin.");
            return;
        }

        if (string.IsNullOrWhiteSpace(AmountTextBox.Text) || !decimal.TryParse(AmountTextBox.Text, out decimal amount))
        {
            MessageBox.Show("Lütfen geçerli bir tutar girin.");
            return;
        }

        if (string.IsNullOrWhiteSpace(CategoryTextBox.Text))
        {
            MessageBox.Show("Lütfen bir kategori girin.");
            return;
        }

        if (TransactionDatePicker.SelectedDate == null)
        {
            MessageBox.Show("Lütfen bir tarih seçin.");
            return;
        }

        try
        {
            // 2. Verileri Topla
            // ComboBoxItem'dan Tag değerini (Income/Expense) alıyoruz
            var selectedTypeItem = (ComboBoxItem)TypeComboBox.SelectedItem;
            string type = selectedTypeItem.Tag.ToString()!; 

            var transaction = new Transaction
            {
                UserId = _currentUserId,
                Amount = amount,
                Type = type,
                Category = CategoryTextBox.Text,
                Description = DescriptionTextBox.Text,
                TransactionDate = TransactionDatePicker.SelectedDate.Value
            };

            // 3. Veritabanına Kaydet
            _userRepository.AddTransaction(transaction);

            MessageBox.Show("İşlem başarıyla kaydedildi! 🎉");
            LoadTransactions();
            
            // 4. Formu Temizle (Bir sonraki işlem için)
            AmountTextBox.Clear();
            CategoryTextBox.Clear();
            DescriptionTextBox.Clear();
            TypeComboBox.SelectedIndex = -1;
            TransactionDatePicker.SelectedDate = DateTime.Now;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata oluştu: {ex.Message}");
        }
    }
    private void LoadTransactions()
    {
        // Son 10 işlemi getir
        var transactions = _userRepository.GetLastTransactions(_currentUserId, 10);
    
        // Listeye bağla
        TransactionsList.ItemsSource = transactions;
    }
}