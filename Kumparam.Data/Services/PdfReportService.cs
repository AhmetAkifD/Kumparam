using System;
using System.Collections.Generic;
using System.Linq;
using Kumparam.Core;
using Kumparam.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// GÜNCELLEME: Transaction sınıfının yeri düzeltildi
using Transaction = Kumparam.Core.Transaction; 

namespace Kumparam.UI.Services
{
    public class PdfReportService
    {
        static PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // GÜNCELLEME: 'reportPeriod' parametresi eklendi
        public void GeneratePdf(string filePath, UserProfile user, List<Transaction> transactions, string reportPeriod)
        {
            var totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            var netBalance = totalIncome - totalExpense;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // 1. ÜST BİLGİ (HEADER)
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("KUMPARAM").FontSize(24).SemiBold().FontColor(Colors.Green.Darken2);
                            col.Item().Text("Finansal Hareket Raporu").FontSize(14).FontColor(Colors.Grey.Darken1);
                            // GÜNCELLEME: Rapor periyodu buraya eklendi
                            col.Item().Text(reportPeriod).FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            var userName = user != null ? $"{user.FirstName} {user.LastName}" : "Kullanıcı";
                            col.Item().Text(userName).FontSize(14).SemiBold();
                            col.Item().Text($"Oluşturulma: {DateTime.Now:dd.MM.yyyy}").FontSize(10);
                        });
                    });

                    // 2. İÇERİK
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // Özet Kutuları
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Component(new StatComponent("Toplam Gelir", totalIncome, Colors.Green.Lighten4, Colors.Green.Darken2));
                            row.Spacing(20);
                            row.RelativeItem().Component(new StatComponent("Toplam Gider", totalExpense, Colors.Red.Lighten4, Colors.Red.Darken2));
                            row.Spacing(20);
                            row.RelativeItem().Component(new StatComponent("Net Durum", netBalance, Colors.Blue.Lighten4, Colors.Blue.Darken2));
                        });

                        col.Item().PaddingVertical(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Tablo
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.RelativeColumn();
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(80);
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
                                var dateStr = item.TransactionDate.ToString("dd.MM.yyyy");

                                table.Cell().Element(BlockStyle).Text(dateStr);
                                table.Cell().Element(BlockStyle).Text(item.Category ?? "-");
                                table.Cell().Element(BlockStyle).Text(item.Description ?? "-");
                                table.Cell().Element(BlockStyle).AlignRight().Text($"{sign} {item.Amount:N2} ₺").FontColor(color).SemiBold();

                                static IContainer BlockStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                                }
                            }
                        });
                    });

                    // 3. ALT BİLGİ
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            })
            .GeneratePdf(filePath);
        }
    }

    public class StatComponent : IComponent
    {
        private string Title { get; }
        private decimal Value { get; }
        private string BgColor { get; }
        private string TextColor { get; }

        public StatComponent(string title, decimal value, string bgColor, string textColor)
        {
            Title = title;
            Value = value;
            BgColor = bgColor;
            TextColor = textColor;
        }

        public void Compose(IContainer container)
        {
            container.Background(BgColor).CornerRadius(5).Padding(10).Column(column =>
            {
                column.Item().Text(Title).FontSize(10).FontColor(Colors.Grey.Darken2);
                column.Item().Text($"{Value:N2} ₺").FontSize(16).SemiBold().FontColor(TextColor);
            });
        }
    }
}