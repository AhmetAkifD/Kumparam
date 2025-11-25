using System;
using System.Windows.Controls;
using System.Configuration; // App.config okumak için
using Kumparam.Core;
using Kumparam.Data;

namespace Kumparam.Pages.DashboardSubPages;

public partial class SummaryView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    // Yeni: Kullanıcı ID'si alan constructor
    public SummaryView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        // Veritabanı bağlantısını başlat
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Verileri Yükle
        LoadData();
    }

    // Parametresiz constructor (XAML editörü hata vermesin diye boş bırakıyoruz)
    public SummaryView()
    {
        InitializeComponent();
    }

    private void LoadData()
    {
        // 1. Veriyi Çek
        var summary = _userRepository.GetFinancialSummary(_currentUserId);

        // 2. Ekrana Yazdır (XAML'deki x:Name'leri kullanıyoruz)
        TotalBalanceText.Text = $"₺ {summary.TotalBalance:N2}";
        MonthlyIncomeText.Text = $"₺ {summary.MonthlyIncome:N2}";
        MonthlyExpenseText.Text = $"₺ {summary.MonthlyExpense:N2}";
        
        // Tasarruf barı
        SavingsProgressBar.Value = (double)summary.SavingsGoalProgress;
        SavingsText.Text = $"%{summary.SavingsGoalProgress} Tamamlandı";
    }
}