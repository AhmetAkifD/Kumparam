namespace Kumparam.Core.Models
{
    public class Investment
    {
        public Guid InvestmentId { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty; 
        public string? Symbol { get; set; }              
        public decimal Quantity { get; set; }            
        public decimal BuyingPrice { get; set; }         
        public decimal? CurrentPrice { get; set; }       
        public DateTime PurchaseDate { get; set; }
        
        // YENİ: Yatırımın kaynağı (ZiraatBankasi veya Web)
        public string Source { get; set; } = "Web"; 

        // --- HESAPLANAN ÖZELLİKLER ---
        public decimal TotalCost => Quantity * BuyingPrice;
        public decimal CurrentTotalValue => Quantity * (CurrentPrice ?? BuyingPrice);
        public decimal ProfitLossAmount => CurrentTotalValue - TotalCost;
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