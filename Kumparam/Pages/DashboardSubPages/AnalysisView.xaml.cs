using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF; 
using SkiaSharp; 
using LiveChartsCore.SkiaSharpView.VisualElements; 

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
        // GÖRÜNÜRLÜK YÖNETİMİ (TOGGLE & CLOSE)
        // ---------------------------------------------------------

        private void ToggleChart_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                if (chk.Name == "TogglePie" && CardPie != null) CardPie.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleBar" && CardBar != null) CardBar.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleLine" && CardLine != null) CardLine.Visibility = Visibility.Visible;
                if (chk.Name == "ToggleDays" && CardDays != null) CardDays.Visibility = Visibility.Visible;
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
            }
        }

        private void CloseCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string chartTag)
            {
                switch (chartTag)
                {
                    case "Pie":
                        TogglePie.IsChecked = false; 
                        break;
                    case "Bar":
                        ToggleBar.IsChecked = false;
                        break;
                    case "Line":
                        ToggleLine.IsChecked = false;
                        break;
                    case "Days":
                        ToggleDays.IsChecked = false;
                        break;
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

                // --- 1. PASTA GRAFİK (GİDERLER) ---
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
                            DataLabelsFormatter = point => $"{point.PrimaryValue:N0}₺",
                            ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.PrimaryValue:N2} ₺"
                        });
                    }

                    PieChartContainer.Content = new PieChart
                    {
                        Series = pieSeries,
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
                    };
                }
                else
                {
                    PieChartContainer.Visibility = Visibility.Collapsed;
                    TxtNoPieData.Visibility = Visibility.Visible;
                }

                // --- 2. SÜTUN GRAFİK (SON 6 AY) ---
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.Now.AddMonths(-5 + i))
                    .ToList();

                var incomeValues = new List<decimal>();
                var expenseValues = new List<decimal>();
                var netValues = new List<decimal>();
                var balanceValues = new List<decimal>();
                var labels = new List<string>();

                foreach (var date in last6Months)
                {
                    labels.Add(date.ToString("MMMM")); 

                    var monthTrans = allTransactions
                        .Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year)
                        .ToList();

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
                            Name = "Gelir",
                            Values = incomeValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                            MaxBarWidth = 40,
                            StackGroup = 0
                        },
                        new StackedColumnSeries<decimal>
                        {
                            Name = "Gider",
                            Values = expenseValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.IndianRed),
                            MaxBarWidth = 40,
                            StackGroup = 0
                        },
                        new StackedColumnSeries<decimal>
                        {
                            Name = "Fark (Net)",
                            Values = netValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.DodgerBlue),
                            MaxBarWidth = 40,
                            StackGroup = 1
                        }
                    },
                    XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = labels.ToArray(),
                            LabelsRotation = 0,
                            TextSize = 13,
                            LabelsPaint = new SolidColorPaint(SKColors.Black)
                        }
                    },
                    YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => value.ToString("N0") + "₺",
                            TextSize = 12
                        }
                    },
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None 
                };

                // --- 3. ÇİZGİ GRAFİK (TREND) ---
                LineChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new LineSeries<decimal>
                        {
                            Name = "Toplam Bakiye",
                            Values = balanceValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.Gold.WithAlpha(50)),
                            Stroke = new SolidColorPaint(SKColors.Goldenrod) { StrokeThickness = 4 },
                            GeometrySize = 12,
                            GeometryStroke = new SolidColorPaint(SKColors.Orange),
                            GeometryFill = new SolidColorPaint(SKColors.White),
                            LineSmoothness = 0.5
                        }
                    },
                    XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = labels.ToArray(),
                            TextSize = 13,
                            LabelsPaint = new SolidColorPaint(SKColors.Black)
                        }
                    },
                    YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => value.ToString("N0") + "₺",
                            TextSize = 12
                        }
                    },
                    TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

                // --- 4. GÜNLÜK HARCAMA ALIŞKANLIKLARI (HAFTANIN GÜNLERİ) ---
                // Türkçe Gün İsimleri (Pazartesi'den başlayarak)
                var turkishDays = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
                
                // DayOfWeek enum'ı Pazar(0) ile başlar, biz Pazartesi(1) ile başlatıp Pazar'ı sona atacağız.
                // Verileri (DayOfWeek, ToplamTutar) şeklinde grupla
                var expensesByDay = allTransactions
                    .Where(t => t.Type == "Expense")
                    .GroupBy(t => t.TransactionDate.DayOfWeek)
                    .Select(g => new { Day = g.Key, Total = g.Sum(t => t.Amount) })
                    .ToList();

                var dayValues = new List<decimal>();

                // Pazartesi(1) -> Cumartesi(6) arası dön
                for (int i = 1; i <= 6; i++)
                {
                    var dayData = expensesByDay.FirstOrDefault(d => (int)d.Day == i);
                    dayValues.Add(dayData?.Total ?? 0);
                }
                // En son Pazar(0)'ı ekle
                var sundayData = expensesByDay.FirstOrDefault(d => (int)d.Day == 0);
                dayValues.Add(sundayData?.Total ?? 0);

                DaysChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new ColumnSeries<decimal>
                        {
                            Name = "Toplam Harcama",
                            Values = dayValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.SlateBlue),
                            MaxBarWidth = 50,
                            Rx = 10, // Köşeleri yuvarlatılmış sütunlar
                            Ry = 10
                        }
                    },
                    XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = turkishDays,
                            TextSize = 13,
                            LabelsPaint = new SolidColorPaint(SKColors.Black)
                        }
                    },
                    YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => value.ToString("N0") + "₺",
                            TextSize = 12
                        }
                    },
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None
                };

            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafik Hatası: " + ex.Message);
            }
        }
    }
}