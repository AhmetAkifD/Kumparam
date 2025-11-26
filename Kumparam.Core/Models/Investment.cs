namespace Kumparam.Core.Models
{
    public class Investment
    {
        public Guid InvestmentId { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty; // Örn: Dolar, Gram Altın
        public string? Symbol { get; set; }              // Örn: USD, GLD
        public decimal Quantity { get; set; }            // Örn: 100 (Dolar) veya 5.5 (Gram)
        public decimal BuyingPrice { get; set; }         // Alış Fiyatı (Birim)
        public decimal? CurrentPrice { get; set; }       // Güncel Fiyat (Birim) - Null olabilir
        public DateTime PurchaseDate { get; set; }

        // --- HESAPLANAN ÖZELLİKLER (Arayüz İçin) ---

        // Toplam Maliyet (Ne kadar ödedim?)
        public decimal TotalCost => Quantity * BuyingPrice;

        // Güncel Toplam Değer (Şu an ne kadar ediyor?)
        // Eğer güncel fiyat yoksa, alış fiyatını baz al (henüz veri çekilmediyse zarar/kâr 0 görünsün)
        public decimal CurrentTotalValue => Quantity * (CurrentPrice ?? BuyingPrice);

        // Kâr / Zarar Tutarı
        public decimal ProfitLossAmount => CurrentTotalValue - TotalCost;

        // Kâr / Zarar Yüzdesi
        public double ProfitLossPercentage
        {
            get
            {
                if (TotalCost == 0) return 0;
                return (double)((ProfitLossAmount / TotalCost) * 100);
            }
        }
    }
}