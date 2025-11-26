using System;

namespace Kumparam.Core
{
    public class Goal
    {
        public Guid GoalId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        
        // Varsayılan değerleri 0 verdik ki null gelirse patlamasın
        public decimal TargetAmount { get; set; } = 0;
        public decimal CurrentAmount { get; set; } = 0;
        
        public DateTime? Deadline { get; set; }
        public string? Description { get; set; }
        
        // XAML BU ÖZELLİĞİ KULLANIYOR! (O yüzden "kullanılmıyor" uyarısını dikkate alma)
        public double ProgressPercentage 
        {
            get 
            {
                try
                {
                    // 1. Güvenlik Kontrolü: Hedef tutar 0 veya negatifse hesaplama yapma
                    if (TargetAmount <= 0) return 0;

                    // 2. Hesaplama
                    var percent = (double)(CurrentAmount / TargetAmount) * 100;

                    // 3. Sınır Kontrolü: %100'ü geçmesin, %0'ın altına düşmesin
                    if (percent > 100) return 100;
                    if (percent < 0) return 0;

                    return percent;
                }
                catch
                {
                    // Ne olursa olsun hata verme, 0 döndür (Uygulamanın kapanmasını engeller)
                    return 0;
                }
            }
        }
    }
}   