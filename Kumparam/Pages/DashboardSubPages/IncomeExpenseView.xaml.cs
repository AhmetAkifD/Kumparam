using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;
using MaterialDesignThemes.Wpf; // İkonlar için gerekli

namespace Kumparam.Pages.DashboardSubPages;

public partial class IncomeExpenseView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;
    private List<Transaction> _allTransactions;

    public IncomeExpenseView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;
        _userRepository = new SqlUserRepository(ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString);

        // Varsayılan Ayarlar
        TransactionDatePicker.SelectedDate = DateTime.Now;
        StartDatePicker.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        EndDatePicker.SelectedDate = DateTime.Now;

        LoadTransactions();
    }
    
    public IncomeExpenseView() { InitializeComponent(); } // Designer için

    private void DescriptionTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SaveButton_Click(sender, e);
    }

    private void LoadTransactions()
    {
        try
        {
            _allTransactions = _userRepository.GetAllTransactions(_currentUserId);
            
            // Kategorileri ComboBox'a doldur (Tekrarsız)
            var categories = _allTransactions.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();
            FilterCategoryCombo.ItemsSource = categories;

            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Hata: " + ex.Message);
        }
    }

    private void ApplyFilters()
    {
        if (_allTransactions == null) return;
        var query = _allTransactions.AsEnumerable();

        // 1. Tarih Filtreleri
        if (StartDatePicker.SelectedDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date >= StartDatePicker.SelectedDate.Value.Date);
        if (EndDatePicker.SelectedDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date <= EndDatePicker.SelectedDate.Value.Date);

        // 2. Tür (Gelir/Gider) Filtresi
        if (FilterTypeCombo.SelectedItem is ComboBoxItem typeItem && typeItem.Tag.ToString() != "All")
        {
            query = query.Where(t => t.Type == typeItem.Tag.ToString());
        }

        // 3. Kategori Filtresi
        if (FilterCategoryCombo.SelectedItem != null)
        {
            string selectedCat = FilterCategoryCombo.SelectedItem.ToString();
            query = query.Where(t => t.Category == selectedCat);
        }

        var filteredList = query.OrderByDescending(t => t.TransactionDate).ToList();
        TransactionsGrid.ItemsSource = filteredList;

        // Özet Hesapla
        decimal totalIncome = filteredList.Where(t => t.Type == "Income").Sum(t => t.Amount);
        decimal totalExpense = filteredList.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        TotalIncomeText.Text = $"{totalIncome:N2} ₺";
        TotalExpenseText.Text = $"{totalExpense:N2} ₺";
        NetBalanceText.Text = $"{totalIncome - totalExpense:N2} ₺";
    }
    
    // --- AKILLI BUTON MANTIĞI (Scroll Sorunu Çözülmüş Hali) ---

    // 1. Buton Yüklendiğinde (İlk Oluşum)
    private void ActionButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            // Veri değişimini dinlemeye başla (Scroll yapınca burası tetiklenecek)
            btn.DataContextChanged -= ActionButton_DataContextChanged; // Çift eklemeyi önle
            btn.DataContextChanged += ActionButton_DataContextChanged;
            
            // İlk görünüm için güncelle
            UpdateActionButton(btn);
        }
    }

    // 2. Scroll Yapınca (Veri Değişince) Tetiklenen Olay
    private void ActionButton_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Button btn)
        {
            UpdateActionButton(btn);
        }
    }

    // 3. İkonu ve İşlevi Güncelleyen Ana Metot
    private void UpdateActionButton(Button btn)
    {
        // Eğer butonun içi boşsa veya geçerli bir işlem (Transaction) yoksa çık
        if (btn.DataContext is not Transaction transaction) return;

        var icon = new PackIcon();
        icon.Width = 20;
        icon.Height = 20;

        // Karar Ver: Link mi Çöp Kutusu mu?
        if (ShouldRedirect(transaction))
        {
            // Yönlendirme Modu
            icon.Kind = PackIconKind.OpenInNew;
            icon.Foreground = Brushes.DodgerBlue;
            btn.ToolTip = "Kaynağa Git (Düzenleme kısıtlı)";
            btn.Cursor = Cursors.Hand;
        }
        else
        {
            // Silme Modu
            icon.Kind = PackIconKind.Delete;
            icon.Foreground = Brushes.Red;
            btn.ToolTip = "Kaydı Sil";
            btn.Cursor = Cursors.Hand;
        }

        btn.Content = icon;
    }

    // 4. Karar Motoru (Aynı Kalabilir)
    private bool ShouldRedirect(Transaction t)
    {
        if (t == null) return false; // Null kontrolü ekledik
        
        string cat = t.Category?.ToLower(new System.Globalization.CultureInfo("tr-TR")) ?? "";
        string type = t.Type; 

        if (cat.Contains("yatırım")) return true;

        if (cat.Contains("hedef") || cat.Contains("birikim"))
        {
            if (type == "Expense") return true; 
            if (type == "Income") return false; 
        }

        return false;
    }

    // 5. Tıklama Olayı (Aynı Kalabilir)
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        // ... (Bu kısım önceki kodla aynı, değiştirmene gerek yok) ...
        if (sender is Button btn && btn.DataContext is Transaction transaction) // Tag yerine DataContext kullanmak daha güvenlidir
        {
             if (ShouldRedirect(transaction))
            {
                NavigateToSource(transaction.Category);
            }
            else
            {
                if (MessageBox.Show("Bu kaydı silmek istediğinize emin misiniz?", "Kayıt Silme", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _userRepository.DeleteTransaction(transaction.TransactionId);
                        LoadTransactions();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Silme hatası: " + ex.Message);
                    }
                }
            }
        }
    }
    // 4. Sayfa Yönlendirmesi
    private void NavigateToSource(string category)
    {
        string cat = category.ToLower(new System.Globalization.CultureInfo("tr-TR"));
        UserControl targetPage = null;

        if (cat.Contains("yatırım")) 
        {
            targetPage = new InvestmentsView(_currentUserId);
        }
        else if (cat.Contains("birikim") || cat.Contains("hedef")) 
        {
            targetPage = new GoalsView(_currentUserId);
        }

        if (targetPage != null)
        {
            // Dashboard'un ana içerik alanını bul ve değiştir
            var dashboard = Window.GetWindow(this) as DashboardWindow;
            if (dashboard != null)
            {
                dashboard.MainContentArea.Content = targetPage;
            }
        }
        else
        {
            MessageBox.Show("İlgili sayfa bulunamadı.");
        }
    }

    // Hangi kategorilerin "Dokunulmaz" olduğunu belirle
    private bool IsSystemTransaction(string category)
    {
        // Kategori isminde bu kelimeler geçiyorsa sistem işlemidir
        string cat = category.ToLower();
        return cat.Contains("yatırım") || cat.Contains("birikim") || cat.Contains("hedef");
    }
    
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;
        FilterTypeCombo.SelectedIndex = 0;
        FilterCategoryCombo.SelectedIndex = -1;
        ApplyFilters();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (InputTypeComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(AmountTextBox.Text) || string.IsNullOrWhiteSpace(CategoryTextBox.Text))
        {
            MessageBox.Show("Lütfen Tipi, Tutarı ve Kategoriyi doldurun.");
            return;
        }

        if (decimal.TryParse(AmountTextBox.Text, out decimal amount))
        {
            var type = ((ComboBoxItem)InputTypeComboBox.SelectedItem).Tag.ToString();
            _userRepository.AddTransaction(new Transaction
            {
                UserId = _currentUserId,
                Amount = amount,
                Type = type!,
                Category = CategoryTextBox.Text,
                Description = DescriptionTextBox.Text,
                TransactionDate = TransactionDatePicker.SelectedDate ?? DateTime.Now
            });

            MessageBox.Show("Kaydedildi!");
            AmountTextBox.Clear(); CategoryTextBox.Clear(); DescriptionTextBox.Clear();
            LoadTransactions();
        }
    }
}