using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data;
using Kumparam.Data.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF; 
using LiveChartsCore.Measure; // LegendPosition ve TooltipPosition için BU ŞART
using SkiaSharp;

namespace Kumparam.Pages.DashboardSubPages;

public partial class SummaryView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    // Dolu Constructor
    public SummaryView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        LoadData();
    }

    // Boş Constructor
    public SummaryView()
    {
        InitializeComponent();
    }

    private void LoadData()
    {
        var summary = _userRepository.GetFinancialSummary(_currentUserId);

        if (summary != null)
        {
            TotalBalanceText.Text = $"₺ {summary.TotalBalance:N2}";
            MonthlyIncomeText.Text = $"₺ {summary.MonthlyIncome:N2}";
            MonthlyExpenseText.Text = $"₺ {summary.MonthlyExpense:N2}";
            
            SavingsProgressBar.Value = (double)summary.SavingsGoalProgress;
            SavingsText.Text = $"%{summary.SavingsGoalProgress:0} Tamamlandı";
        }
        
        try
        {
            // Son 5 işlemi getir
            var transactions = _userRepository.GetLastTransactions(_currentUserId, 5);
            TransactionsList.ItemsSource = transactions;
        }
        catch
        {
            // Hata olursa listeyi boş bırak (Uygulama patlamasın)
            TransactionsList.ItemsSource = null;
        }

        // 4. Grafiği Yükle
        LoadChart();
    }

    private void LoadChart()
    {
        var stats = _userRepository.GetExpenseStats(_currentUserId);

        // Veri yoksa
        if (stats.Count == 0)
        {
            ChartContainer.Visibility = Visibility.Collapsed;
            NoDataText.Visibility = Visibility.Visible;
            return;
        }

        // Veri varsa
        ChartContainer.Visibility = Visibility.Visible;
        NoDataText.Visibility = Visibility.Collapsed;

        // 1. Serileri Hazırla
        var seriesCollection = new List<ISeries>();

        foreach (var item in stats)
        {
            seriesCollection.Add(new PieSeries<decimal>
            {
                Values = new decimal[] { item.TotalAmount },
                Name = item.Category,
                InnerRadius = 50,
                HoverPushout = 10,
                
                // Yüzdelik gösterim
                DataLabelsFormatter = point => $"{point.StackedValue.Share:P0}", 
                
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsPaint = new SolidColorPaint(SKColors.White)
            });
        }

        // 2. Grafiği Kodla Oluştur
        var chart = new PieChart
        {
            Series = seriesCollection,
            // LegendPosition ve TooltipPosition için "LiveChartsCore.Measure" namespace'i eklendi
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
            TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top
        };

        // 3. Kutunun içine koy
        ChartContainer.Content = chart;
    }
}