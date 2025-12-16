using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // WPF Colors
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.SkiaSharpView.WPF; // WPF'e özel chartlar burada
using SkiaSharp; // Renkler için

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

            // Sayfa yüklendiğinde grafikleri oluştur
            LoadCharts();
        }

        private void LoadCharts()
        {
            try
            {
                // 1. Tüm verileri çek (SQL ile uğraşmadan C# tarafında süzeceğiz)
                var allTransactions = _userRepository.GetAllTransactions(_currentUserId);

                // --- PASTA GRAFİK (GİDERLER) ---
                var expenseData = allTransactions
                    .Where(t => t.Type == "Expense")
                    .GroupBy(t => t.Category)
                    .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                if (expenseData.Any())
                {
                    // LiveCharts Serisi Oluşturma
                    var pieSeries = new List<ISeries>();

                    foreach (var item in expenseData)
                    {
                        pieSeries.Add(new PieSeries<decimal>
                        {
                            Values = new decimal[] { item.Total },
                            Name = item.Category,
                            DataLabelsSize = 12,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),
                            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                            DataLabelsFormatter = point => $"{point.PrimaryValue:N0}₺",
                            ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.PrimaryValue:N2} ₺"
                        });
                    }

                    // XAML'daki Konteynere Grafiği Ekle
                    PieChartContainer.Content = new PieChart
                    {
                        Series = pieSeries,
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
                        Title = new LabelVisual { Text = "Harcama Dağılımı", TextSize = 15 }
                    };
                }
                else
                {
                    PieChartContainer.Visibility = Visibility.Collapsed;
                    TxtNoPieData.Visibility = Visibility.Visible;
                }

                // --- SÜTUN GRAFİK (GELİR vs GİDER - SON 6 AY) ---
                
                // Son 6 ayın isimlerini al (Örn: "Kasım", "Aralık"...)
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.Now.AddMonths(-5 + i))
                    .ToList();

                var incomeValues = new List<decimal>();
                var expenseValues = new List<decimal>();
                var labels = new List<string>();

                foreach (var date in last6Months)
                {
                    labels.Add(date.ToString("MMMM")); // Ay İsmi

                    // O ayın verilerini filtrele
                    var monthTrans = allTransactions
                        .Where(t => t.TransactionDate.Month == date.Month && t.TransactionDate.Year == date.Year)
                        .ToList();

                    incomeValues.Add(monthTrans.Where(t => t.Type == "Income").Sum(t => t.Amount));
                    expenseValues.Add(monthTrans.Where(t => t.Type == "Expense").Sum(t => t.Amount));
                }

                // Code-Behind'da CartesianChart oluşturma
                BarChartContainer.Content = new CartesianChart
                {
                    Series = new ISeries[]
                    {
                        new ColumnSeries<decimal>
                        {
                            Name = "Gelir",
                            Values = incomeValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.MediumSeaGreen), // Yeşil
                            MaxBarWidth = 30
                        },
                        new ColumnSeries<decimal>
                        {
                            Name = "Gider",
                            Values = expenseValues.ToArray(),
                            Fill = new SolidColorPaint(SKColors.IndianRed), // Kırmızı
                            MaxBarWidth = 30
                        }
                    },
                    XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = labels.ToArray(),
                            LabelsRotation = 0,
                            TextSize = 12
                        }
                    },
                    YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => value.ToString("N0") + "₺"
                        }
                    },
                    LegendPosition = LiveChartsCore.Measure.LegendPosition.Top
                };

            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafikler oluşturulurken hata: " + ex.Message);
            }
        }
    }
}