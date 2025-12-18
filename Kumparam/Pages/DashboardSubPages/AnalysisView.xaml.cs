using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF; 
using SkiaSharp; 
using LiveChartsCore.SkiaSharpView.VisualElements; 
using Microsoft.Win32;
using Kumparam.UI.Services;
using Kumparam.Core.Models;
using Kumparam.UI.Helpers;

namespace Kumparam.Pages.DashboardSubPages
{
    public partial class AnalysisView : UserControl
    {
        private readonly IUserRepository _userRepository;
        private readonly Guid _currentUserId;

        public AnalysisView(Guid userId)
        {
            InitializeComponent();
            _currentUserId = userId;

            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);

            LoadCharts();
        }

        // ---------------------------------------------------------
        // GÖRÜNÜRLÜK YÖNETİMİ
        // ---------------------------------------------------------
        private void ToggleChart_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                if (chk.Name == "TogglePie" && CardPie != null) CardPie.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleBar" && CardBar != null) CardBar.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleLine" && CardLine != null) CardLine.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleDays" && CardDays != null) CardDays.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleStacked" && CardStacked != null) CardStacked.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleWaterfall" && CardWaterfall != null) CardWaterfall.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleBudget" && CardBudget != null) CardBudget.Visibility = Visibility.Visible;
            }
        }

        private void ToggleChart_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                if (chk.Name == "TogglePie" && CardPie != null) CardPie.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleBar" && CardBar != null) CardBar.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleLine" && CardLine != null) CardLine.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleDays" && CardDays != null) CardDays.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleStacked" && CardStacked != null) CardStacked.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleWaterfall" && CardWaterfall != null) CardWaterfall.Visibility = Visibility.Collapsed;
                if (chk.Name == "ToggleBudget" && CardBudget != null) CardBudget.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string chartTag)
            {
                switch (chartTag)
                {
                    case "Pie": TogglePie.IsChecked = false; break;
                    case "Bar": ToggleBar.IsChecked = false; break;
                    case "Line": ToggleLine.IsChecked = false; break;
                    case "Days": ToggleDays.IsChecked = false; break;
                    case "Stacked": ToggleStacked.IsChecked = false; break;
                    case "Waterfall": ToggleWaterfall.IsChecked = false; break;
                    case "Budget": ToggleBudget.IsChecked = false; break;
                }
            }
        }

        // ---------------------------------------------------------
        // GRAFİK OLUŞTURMA
        // ---------------------------------------------------------
        private void LoadCharts()
        {
            try
            {
                var allTransactions = _userRepository.GetAllTransactions(_currentUserId);

                // --- 1. PASTA GRAFİK ---
                var expenseData = allTransactions
                    .Where(t => t.Type == "Expense")
                    .GroupBy(t => t.Category)
                    .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                if (expenseData.Any())
                {
                    var pieSeries = new List<ISeries>();
                    foreach (var item in expenseData)
                    {
                        pieSeries.Add(new PieSeries<decimal>
                        {
                            Values = new decimal[] { item.Total },
                            Name = item.Category,
                            InnerRadius = 25, 
                            DataLabelsSize = 12,
                            DataLabelsPaint = new SolidColorPaint(SKColors.Black), 
                            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer, 
                            DataLabelsFormatter = point => $"{point.PrimaryValue:N0}₺"
                            // PieSeries için ToolTipLabelFormatter doğru, değiştirmeye gerek yok.
                        });
                    }
                    PieChartContainer.Content = new PieChart
                    {
                        Series = pieSeries,
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right, 
                    };
                }
                else
                {
                    PieChartContainer.Visibility = Visibility.Collapsed;
                    TxtNoPieData.Visibility = Visibility.Visible;
                }

                // --- 2. SÜTUN GRAFİK (6 Ay) ---
                var last6Months = Enumerable.Range(0, 6).Select(i => DateTime.Now.AddMonths(-5 + i)).ToList();
                var labels = new List<string>();
                var incomeValues = new List<decimal>();
                var expenseValues = new List<decimal>();
                var netValues = new List<decimal>();
                var balanceValues = new List<decimal>();

                foreach (var date in last6Months)
                {
                    labels.Add(date.ToString("MMMM")); 
                    var monthTrans = allTransactions.Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year).ToList();

                    decimal income = monthTrans.Where(t => t.Type == "Income").Sum(t => t.Amount);
                    decimal expense = monthTrans.Where(t => t.Type == "Expense").Sum(t => t.Amount) * -1;
                    
                    incomeValues.Add(income);
                    expenseValues.Add(expense);
                    netValues.Add(income + expense);

                    var cumulativeDate = new DateTime(date.Year, date.Month, 1).AddMonths(1).AddDays(-1);
                    var cumulativeIncome = allTransactions.Where(t => t.TransactionDate <= cumulativeDate && t.Type == "Income").Sum(t => t.Amount);
                    var cumulativeExpense = allTransactions.Where(t => t.TransactionDate <= cumulativeDate && t.Type == "Expense").Sum(t => t.Amount);
                    balanceValues.Add(cumulativeIncome - cumulativeExpense);
                }
                
                BarChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new StackedColumnSeries<decimal> 
                        { 
                            Name = "Gelir", Values = incomeValues.ToArray(), Fill = new SolidColorPaint(SKColors.MediumSeaGreen), MaxBarWidth = 25, StackGroup = 0
                        },
                        new StackedColumnSeries<decimal> 
                        { 
                            Name = "Gider", Values = expenseValues.ToArray(), Fill = new SolidColorPaint(SKColors.IndianRed), MaxBarWidth = 25, StackGroup = 0,
                        },
                        new StackedColumnSeries<decimal> 
                        { 
                            Name = "Fark", Values = netValues.ToArray(), Fill = new SolidColorPaint(SKColors.DodgerBlue), MaxBarWidth = 25, StackGroup = 1,
                        }
                    },
                    XAxes = new Axis[] { new Axis { Labels = labels.ToArray(), TextSize = 12, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                    YAxes = new Axis[] { new Axis { Labeler = value => value.ToString("N0") + "₺", TextSize = 12 } },
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

                // --- 3. YIĞILMIŞ SÜTUN (Aylık Kategori Detayı) ---
                var stackedSeries = new List<ISeries>();
                var topCategories = allTransactions
                    .Where(t => t.Type == "Expense" && t.TransactionDate >= DateTime.Now.AddMonths(-6))
                    .GroupBy(t => t.Category)
                    .Select(g => new { Cat = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .Take(5) 
                    .Select(x => x.Cat)
                    .ToList();

                foreach (var cat in topCategories)
                {
                    var catValues = new List<decimal>();
                    foreach (var date in last6Months)
                    {
                        var sum = allTransactions
                            .Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year 
                                        && t.Type == "Expense" && t.Category == cat)
                            .Sum(t => t.Amount);
                        catValues.Add(sum); 
                    }

                    stackedSeries.Add(new StackedColumnSeries<decimal>
                    {
                        Name = cat,
                        Values = catValues.ToArray(),
                        MaxBarWidth = 40,
                        DataLabelsSize = 0
                    });
                }

                var otherValues = new List<decimal>();
                bool hasOthers = false;
                foreach (var date in last6Months)
                {
                    var sum = allTransactions
                        .Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year 
                                    && t.Type == "Expense" && !topCategories.Contains(t.Category))
                        .Sum(t => t.Amount);
                    otherValues.Add(sum);
                    if(sum > 0) hasOthers = true;
                }

                if (hasOthers)
                {
                    stackedSeries.Add(new StackedColumnSeries<decimal>
                    {
                        Name = "Diğer",
                        Values = otherValues.ToArray(),
                        Fill = new SolidColorPaint(SKColors.Gray),
                        MaxBarWidth = 40,
                        DataLabelsSize = 0,
                    });
                }

                StackedChartContainer.Content = new CartesianChart
                {
                    Series = stackedSeries,
                    XAxes = new Axis[] { new Axis { Labels = labels.ToArray(), TextSize = 12, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                    YAxes = new Axis[] { new Axis { Labeler = value => value.ToString("N0") + "₺", TextSize = 12 } },
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Right, 
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

                // --- 4. WATERFALL (Bu Ayın Bütçe Akışı) ---
                var thisMonth = DateTime.Now;
                var thisMonthTrans = allTransactions.Where(t => t.TransactionDate.Month == thisMonth.Month && t.TransactionDate.Year == thisMonth.Year).ToList();
                
                decimal thisMonthIncome = thisMonthTrans.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal thisMonthRemaining = thisMonthIncome - thisMonthTrans.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                var waterfallSeries = new List<ISeries>();

                // Sütun 1: Gelir
                waterfallSeries.Add(new StackedColumnSeries<decimal> 
                { 
                    Name = "Gelir", Values = new decimal[] { thisMonthIncome, 0, 0 }, 
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen), 
                    Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    MaxBarWidth = 50, StackGroup = 0
                });

                // Sütun 2: Giderler (Top 4 + Diğer)
                var expensesGroups = thisMonthTrans.Where(t => t.Type == "Expense")
                    .GroupBy(t => t.Category)
                    .Select(g => new { Cat = g.Key, Sum = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Sum)
                    .ToList();

                var topWaterfall = expensesGroups.Take(4).ToList(); 
                var otherWaterfall = expensesGroups.Skip(4).Sum(x => x.Sum); 

                foreach (var item in topWaterfall)
                {
                    waterfallSeries.Add(new StackedColumnSeries<decimal> 
                    { 
                        Name = item.Cat, Values = new decimal[] { 0, item.Sum, 0 }, 
                        MaxBarWidth = 50, StackGroup = 0,
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 }
                    });
                }

                if (otherWaterfall > 0)
                {
                    waterfallSeries.Add(new StackedColumnSeries<decimal> 
                    { 
                        Name = "Diğer Giderler", Values = new decimal[] { 0, otherWaterfall, 0 }, 
                        Fill = new SolidColorPaint(SKColors.Gray),
                        MaxBarWidth = 50, StackGroup = 0,
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 }
                    });
                }

                // Sütun 3: Kalan
                waterfallSeries.Add(new StackedColumnSeries<decimal> 
                { 
                    Name = "Kalan", Values = new decimal[] { 0, 0, thisMonthRemaining }, 
                    Fill = new SolidColorPaint(thisMonthRemaining >= 0 ? SKColors.DodgerBlue : SKColors.Red), 
                    MaxBarWidth = 50, StackGroup = 0,
                    Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }
                });

                WaterfallChartContainer.Content = new CartesianChart
                {
                    Series = waterfallSeries,
                    XAxes = new Axis[] { new Axis { Labels = new[] { "Toplam Gelir", "Harcamalar", "Kalan Bakiye" }, TextSize = 13, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                    YAxes = new Axis[] { new Axis { Labeler = value => value.ToString("N0") + "₺", TextSize = 12 } },
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Right, 
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

                // --- 5. ÇİZGİ GRAFİK ---
                LineChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new LineSeries<decimal>
                        {
                            Name = "Toplam Bakiye", Values = balanceValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.Gold.WithAlpha(50)), Stroke = new SolidColorPaint(SKColors.Goldenrod) { StrokeThickness = 4 },
                            GeometrySize = 12, GeometryStroke = new SolidColorPaint(SKColors.Orange), GeometryFill = new SolidColorPaint(SKColors.White), LineSmoothness = 0.5
                        }
                    },
                    XAxes = new Axis[] { new Axis { Labels = labels.ToArray(), TextSize = 13, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                    YAxes = new Axis[] { new Axis { Labeler = value => value.ToString("N0") + "₺", TextSize = 12 } },
                    TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

                // --- 6. GÜNLÜK HARCAMA ---
                var turkishDays = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
                var expensesByDay = allTransactions.Where(t => t.Type == "Expense").GroupBy(t => t.TransactionDate.DayOfWeek).Select(g => new { Day = g.Key, Total = g.Sum(t => t.Amount) }).ToList();
                var dayValues = new List<decimal>();
                for (int i = 1; i <= 6; i++) dayValues.Add(expensesByDay.FirstOrDefault(d => (int)d.Day == i)?.Total ?? 0);
                dayValues.Add(expensesByDay.FirstOrDefault(d => (int)d.Day == 0)?.Total ?? 0);

                DaysChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new ColumnSeries<decimal> 
                        { 
                            Name = "Toplam Harcama", Values = dayValues.ToArray(), Fill = new SolidColorPaint(SKColors.SlateBlue), MaxBarWidth = 50, Rx = 10, Ry = 10
                        }
                    },
                    XAxes = new Axis[] { new Axis { Labels = turkishDays, TextSize = 13, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                    YAxes = new Axis[] { new Axis { Labeler = value => value.ToString("N0") + "₺", TextSize = 12 } },
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };
                CalculateBudgetRule(allTransactions);


            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafik Hatası: " + ex.Message);
            }
        }
        private void DownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            // Rapor Filtreleme Penceresini Aç
            Window reportWindow = new Window
            {
                Title = "Rapor Oluştur",
                Content = new ReportDialogView(_currentUserId), // Yeni oluşturduğumuz view
                Width = 600,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, // Çerçevesiz modern görünüm (İstersen SingleBorderWindow yapabilirsin)
                AllowsTransparency = true,      // Köşeleri yuvarlatmak için şeffaflık lazım
                Background = System.Windows.Media.Brushes.Transparent
            };

            reportWindow.ShowDialog();
        }
        private void CalculateBudgetRule(List<Transaction> transactions)
        {
            // Bu ayın verilerine odaklanalım
            var thisMonth = DateTime.Now;
            var monthTrans = transactions
                .Where(t => t.TransactionDate.Month == thisMonth.Month && t.TransactionDate.Year == thisMonth.Year)
                .ToList();

            decimal totalIncome = monthTrans.Where(t => t.Type == "Income").Sum(t => t.Amount);
            
            // Gelir yoksa hesaplama yapma (Sıfıra bölünme hatası olmasın)
            if (totalIncome == 0)
            {
                TxtBudgetComment.Text = "Bu ay henüz gelir kaydı girilmediği için analiz yapılamıyor.";
                PbNeeds.Value = 0; PbWants.Value = 0; PbSavings.Value = 0;
                TxtNeeds.Text = "%0"; TxtWants.Text = "%0"; TxtSavings.Text = "%0";
                return;
            }

            decimal needsTotal = 0;
            decimal wantsTotal = 0;
            decimal savingsTotal = 0;

            // Giderleri Sınıflandır
            foreach (var expense in monthTrans.Where(t => t.Type == "Expense"))
            {
                var type = BudgetHelper.GetBudgetType(expense.Category);
                if (type == BudgetType.Needs) needsTotal += expense.Amount;
                else if (type == BudgetType.Wants) wantsTotal += expense.Amount;
                else if (type == BudgetType.Savings) savingsTotal += expense.Amount;
                else wantsTotal += expense.Amount; // Bilinmeyenleri isteklere at (Kötümser yaklaşım)
            }

            // Kalan bakiyeyi de "Potansiyel Birikim" sayabiliriz ama şimdilik sadece harcananları baz alalım.
            // VEYA: Gelirden giderleri düştükten sonra kalanı "Savings"e ekleyebilirsin.
            // Ben şimdilik sadece kategorize edilmiş harcamaları gösteriyorum.

            // Yüzdeleri Hesapla
            double needsPercent = (double)(needsTotal / totalIncome) * 100;
            double wantsPercent = (double)(wantsTotal / totalIncome) * 100;
            double savingsPercent = (double)(savingsTotal / totalIncome) * 100;

            // UI Güncelle
            PbNeeds.Value = needsPercent;
            TxtNeeds.Text = $"%{needsPercent:0.0} ({needsTotal:N0}₺)";
            
            PbWants.Value = wantsPercent;
            TxtWants.Text = $"%{wantsPercent:0.0} ({wantsTotal:N0}₺)";
            
            PbSavings.Value = savingsPercent;
            TxtSavings.Text = $"%{savingsPercent:0.0} ({savingsTotal:N0}₺)";

            // Renklendirme ve Yorum
            string comment = "";
            
            // İhtiyaç Kontrolü
            if (needsPercent > 50) 
            {
                PbNeeds.Foreground = System.Windows.Media.Brushes.Red;
                comment += "⚠️ İhtiyaç harcamalarınız %50 sınırını aşmış. Sabit giderleri gözden geçirin. ";
            }
            else
            {
                PbNeeds.Foreground = System.Windows.Media.Brushes.Green;
                comment += "✅ İhtiyaçlar dengeli. ";
            }

            // İstek Kontrolü
            if (wantsPercent > 30)
            {
                PbWants.Foreground = System.Windows.Media.Brushes.Red;
                comment += "⚠️ Keyfi harcamalarınız %30'u geçmiş. Biraz tasarruf iyi olabilir. ";
            }
            else
            {
                PbWants.Foreground = System.Windows.Media.Brushes.Orange; // İstekler her zaman turuncu kalsın
                comment += "👍 İstekler kontrol altında. ";
            }

            // Birikim Kontrolü
            if (savingsPercent < 20)
            {
                PbSavings.Foreground = System.Windows.Media.Brushes.Orange; // Uyarı rengi
                comment += "📉 Birikim oranınız %20'nin altında. ";
            }
            else
            {
                PbSavings.Foreground = System.Windows.Media.Brushes.DodgerBlue;
                comment += "💰 Harika! %20 birikim hedefini tutturdunuz. ";
            }

            TxtBudgetComment.Text = comment;
        }
    }
}