using System;
using System.Collections.Generic;
using System.Linq;
using Kumparam.Core;
using Kumparam.Core.Models;
using Kumparam.UI.Helpers; // BudgetHelper için
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using Transaction = Kumparam.Core.Transaction;

namespace Kumparam.UI.Services
{
    public class PdfReportService
    {
        static PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GeneratePdf(string filePath, UserProfile user, List<Transaction> transactions, List<Investment> investments, List<Goal> goals, string reportPeriod)
        {
            // --- 1. HESAPLAMALAR ---

            // Gelir/Gider
            var totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            var netBalance = totalIncome - totalExpense;

            // Yatırım Durumu
            decimal totalInvestmentCost = investments.Sum(i => i.TotalCost);
            decimal totalInvestmentValue = investments.Sum(i => i.CurrentTotalValue); 
            decimal investmentProfit = totalInvestmentValue - totalInvestmentCost;
            
            // Hedef Durumu
            decimal totalGoalSaved = goals.Sum(g => g.CurrentAmount);
            decimal totalGoalTarget = goals.Sum(g => g.TargetAmount);

            // 50/30/20 Analizi
            decimal needsTotal = 0, wantsTotal = 0, savingsTotal = 0;
            
            foreach (var expense in transactions.Where(t => t.Type == "Expense"))
            {
                var type = BudgetHelper.GetBudgetType(expense.Category);
                if (type == BudgetType.Needs) needsTotal += expense.Amount;
                else if (type == BudgetType.Wants) wantsTotal += expense.Amount;
                else savingsTotal += expense.Amount; 
            }

            // --- 2. PDF OLUŞTURMA ---

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // BAŞLIK BÖLÜMÜ
                    page.Content().Column(col =>
                    {
                        // Header
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("KUMPARAM").FontSize(24).SemiBold().FontColor(Colors.Green.Darken2);
                                c.Item().Text("Finansal Durum Raporu").FontSize(14).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(reportPeriod).FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                var userName = user != null ? $"{user.FirstName} {user.LastName}" : "Kullanıcı";
                                c.Item().Text(userName).FontSize(14).SemiBold();
                                c.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy}").FontSize(10);
                            });
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 1. BÖLÜM: VARLIK ÖZETİ
                        col.Item().Text("Varlık Özeti").FontSize(12).SemiBold().FontColor(Colors.Black);
                        col.Item().PaddingVertical(5).Row(row =>
                        {
                            // Net Nakit
                            row.RelativeItem().Component(new StatCard("Net Nakit Akışı", netBalance, Colors.Blue.Lighten5, Colors.Blue.Darken2));
                            row.Spacing(10);
                            // Yatırım Değeri
                            row.RelativeItem().Component(new StatCard("Portföy Değeri", totalInvestmentValue, Colors.Purple.Lighten5, Colors.Purple.Darken2));
                            row.Spacing(10);
                            // Hedef Kumbarası
                            row.RelativeItem().Component(new StatCard("Hedef Kumbarası", totalGoalSaved, Colors.Orange.Lighten5, Colors.Orange.Darken2));
                        });

                        col.Item().PaddingVertical(10);

                        // 2. BÖLÜM: YATIRIM PERFORMANSI & BÜTÇE SAĞLIĞI
                        col.Item().Row(row =>
                        {
                            // Sol: Yatırım Detayı
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Yatırım Performansı").FontSize(11).SemiBold();
                                c.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);
                                
                                c.Item().Row(r => {
                                    r.RelativeItem().Text("Maliyet:");
                                    r.RelativeItem().AlignRight().Text($"{totalInvestmentCost:N2} ₺");
                                });
                                c.Item().Row(r => {
                                    r.RelativeItem().Text("Güncel Değer:");
                                    r.RelativeItem().AlignRight().Text($"{totalInvestmentValue:N2} ₺");
                                });
                                
                                var profitColor = investmentProfit >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2;
                                var profitSign = investmentProfit >= 0 ? "+" : "";
                                
                                c.Item().PaddingTop(5).Row(r => {
                                    r.RelativeItem().Text("Net Kâr/Zarar:").Bold();
                                    r.RelativeItem().AlignRight().Text($"{profitSign}{investmentProfit:N2} ₺").FontColor(profitColor).Bold();
                                });
                            });

                            row.Spacing(20);

                            // Sağ: 50/30/20 Kuralı
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Bütçe Sağlığı (50/30/20)").FontSize(11).SemiBold();
                                c.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);

                                if (totalIncome > 0)
                                {
                                    double needsPct = (double)(needsTotal / totalIncome) * 100;
                                    double wantsPct = (double)(wantsTotal / totalIncome) * 100;
                                    double savingsPct = (double)(savingsTotal / totalIncome) * 100;

                                    c.Item().Component(new BudgetBar("İhtiyaçlar", needsPct, 50, Colors.Green.Medium));
                                    c.Item().Component(new BudgetBar("İstekler", wantsPct, 30, Colors.Orange.Medium));
                                    c.Item().Component(new BudgetBar("Birikim", savingsPct, 20, Colors.Blue.Medium));
                                }
                                else
                                {
                                    c.Item().Text("Gelir verisi olmadığı için hesaplanamadı.").Italic().FontColor(Colors.Grey.Darken1);
                                }
                            });
                        });

                        // -- YAPAY ZEKA BÖLÜMÜ BURADAN SİLİNDİ --
                        
                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 3. BÖLÜM: İŞLEM DÖKÜMÜ (Tablo) - (Numarası kaydı)
                        col.Item().PaddingBottom(5).Text("İşlem Hareketleri").FontSize(12).SemiBold();
                        
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);  // Tarih
                                columns.RelativeColumn();    // Kategori
                                columns.RelativeColumn(2);   // Açıklama
                                columns.ConstantColumn(80);  // Tutar
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Tarih");
                                header.Cell().Element(CellStyle).Text("Kategori");
                                header.Cell().Element(CellStyle).Text("Açıklama");
                                header.Cell().Element(CellStyle).AlignRight().Text("Tutar");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            foreach (var item in transactions)
                            {
                                var color = item.Type == "Income" ? Colors.Green.Darken2 : Colors.Red.Darken2;
                                var sign = item.Type == "Income" ? "+" : "-";

                                table.Cell().Element(BlockStyle).Text($"{item.TransactionDate:dd.MM.yyyy}");
                                table.Cell().Element(BlockStyle).Text(item.Category ?? "-");
                                table.Cell().Element(BlockStyle).Text(item.Description ?? "-");
                                table.Cell().Element(BlockStyle).AlignRight().Text($"{sign} {item.Amount:N2} ₺").FontColor(color).SemiBold();

                                static IContainer BlockStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2);
                                }
                            }
                        });
                    });

                    // FOOTER
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf(filePath);
        }
    }

    // --- YARDIMCI BİLEŞENLER ---

    // 1. Renkli Özet Kutusu
    public class StatCard : IComponent
    {
        private string Title { get; }
        private decimal Value { get; }
        private string BgColor { get; }
        private string TextColor { get; }

        public StatCard(string title, decimal value, string bgColor, string textColor)
        {
            Title = title;
            Value = value;
            BgColor = bgColor;
            TextColor = textColor;
        }

        public void Compose(IContainer container)
        {
            container.Background(BgColor).CornerRadius(5).Padding(10).Column(c =>
            {
                c.Item().Text(Title).FontSize(9).FontColor(Colors.Grey.Darken2);
                c.Item().Text($"{Value:N2} ₺").FontSize(14).SemiBold().FontColor(TextColor);
            });
        }
    }

    // 2. Bütçe Barı (Progress Bar Benzeri)
    public class BudgetBar : IComponent
    {
        private string Label { get; }
        private double Percent { get; }
        private double Target { get; }
        private string ColorHex { get; }

        public BudgetBar(string label, double percent, double target, string colorHex)
        {
            Label = label;
            Percent = percent;
            Target = target;
            ColorHex = colorHex;
        }

        public void Compose(IContainer container)
        {
            container.PaddingBottom(5).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(Label).FontSize(9);
                    r.RelativeItem().AlignRight().Text($"%{Percent:0.0} / %{Target}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                
                c.Item().Height(5).Background(Colors.Grey.Lighten3).Row(r => 
                {
                    double safePercent = Math.Min(Percent, 100);
                    if (safePercent > 0)
                    {
                        r.RelativeItem((float)safePercent).Background(ColorHex);
                    }
                    if (safePercent < 100)
                    {
                        r.RelativeItem((float)(100 - safePercent)); 
                    }
                });
            });
        }
    }
}