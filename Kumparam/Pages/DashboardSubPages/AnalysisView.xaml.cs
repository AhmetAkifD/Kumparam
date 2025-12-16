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
                            InnerRadius = 50, 
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

                // --- 2. SÜTUN GRAFİK (SON 6 AY GELİR/GİDER/FARK) ---
                
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.Now.AddMonths(-5 + i))
                    .ToList();

                var incomeValues = new List<decimal>();
                var expenseValues = new List<decimal>();
                var netValues = new List<decimal>();
                var balanceValues = new List<decimal>(); // Çizgi grafik için
                var labels = new List<string>();

                foreach (var date in last6Months)
                {
                    labels.Add(date.ToString("MMMM")); 

                    // O ayın işlemleri
                    var monthTrans = allTransactions
                        .Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year)
                        .ToList();

                    decimal income = monthTrans.Where(t => t.Type == "Income").Sum(t => t.Amount);
                    decimal expense = monthTrans.Where(t => t.Type == "Expense").Sum(t => t.Amount) * -1;
                    
                    incomeValues.Add(income);
                    expenseValues.Add(expense);
                    netValues.Add(income + expense);

                    // --- ÇİZGİ GRAFİK İÇİN HESAPLAMA ---
                    // O tarihe kadar olan TÜM zamanların toplam bakiyesi
                    // Yani "O ayın sonunda cebimde kaç para vardı?"
                    var cumulativeDate = new DateTime(date.Year, date.Month, 1).AddMonths(1).AddDays(-1); // Ayın son günü
                    
                    var cumulativeIncome = allTransactions
                        .Where(t => t.TransactionDate <= cumulativeDate && t.Type == "Income")
                        .Sum(t => t.Amount);
                        
                    var cumulativeExpense = allTransactions
                        .Where(t => t.TransactionDate <= cumulativeDate && t.Type == "Expense")
                        .Sum(t => t.Amount);

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
                    ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X
                };

                // --- 3. ÇİZGİ GRAFİK (VARLIK GELİŞİMİ) ---
                LineChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new LineSeries<decimal>
                        {
                            Name = "Toplam Bakiye",
                            Values = balanceValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.Gold.WithAlpha(50)), // Altın sarısı dolgu (şeffaf)
                            Stroke = new SolidColorPaint(SKColors.Goldenrod) { StrokeThickness = 4 }, // Çizgi rengi
                            GeometrySize = 12, // Nokta büyüklüğü
                            GeometryStroke = new SolidColorPaint(SKColors.Orange), // Nokta kenarı
                            GeometryFill = new SolidColorPaint(SKColors.White), // Nokta içi
                            LineSmoothness = 0.5 // Yumuşak kıvrımlar (0 = düz çizgi, 1 = çok kıvrımlı)
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
                };

            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafik Hatası: " + ex.Message);
            }
        }
    }
}