using System;
using System.Windows.Controls;
using System.Configuration; // App.config okumak için
using Kumparam.Core;
using Kumparam.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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
    private void LoadChart()
    {
        var stats = _userRepository.GetExpenseStats(_currentUserId);

        // Eğer hiç harcama yoksa boş mesajı göster
        if (stats.Count == 0)
        {
            // Visibility çakışmasını önlemek için tam adını (System.Windows...) yazdık
            ExpensePieChart.Visibility = System.Windows.Visibility.Collapsed;
            NoDataText.Visibility = System.Windows.Visibility.Visible;
            return;
        }

        ExpensePieChart.Visibility = System.Windows.Visibility.Visible;
        NoDataText.Visibility = System.Windows.Visibility.Collapsed;

        // Verileri LiveCharts serisine dönüştür
        var seriesCollection = new List<ISeries>();

        foreach (var item in stats)
        {
            seriesCollection.Add(new PieSeries<decimal>
            {
                Values = new decimal[] { item.TotalAmount },
                Name = item.Category,
                InnerRadius = 50,
            
                // DÜZELTME: HoverPushout buraya, serinin içine taşındı
                HoverPushout = 10, 
            
                // DÜZELTME: Yüzdelik hesaplama için en güvenli yöntem "Share" kullanmaktır
                // :P0 formatı otomatik olarak % işareti koyar ve yuvarlar (örn: %25)
                DataLabelsFormatter = point => $"{point.StackedValue.Share:P0}", 
            
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsPaint = new SolidColorPaint(SKColors.White)
            });
        }

        ExpensePieChart.Series = seriesCollection;
    }
}