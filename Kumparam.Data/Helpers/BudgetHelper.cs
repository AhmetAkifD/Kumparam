using System.Collections.Generic;

namespace Kumparam.UI.Helpers
{
    public enum BudgetType
    {
        Needs,   // İhtiyaç (%50)
        Wants,   // İstek (%30)
        Savings, // Birikim (%20)
        Unknown  // Belirsiz
    }

    public static class BudgetHelper
    {
        // Kategori adlarını küçük harfe çevirip kontrol edeceğiz
        public static BudgetType GetBudgetType(string categoryName)
        {
            var cat = categoryName.ToLower().Trim();

            // 1. İHTİYAÇLAR (%50)
            if (cat.Contains("kira") || cat.Contains("fatura") || cat.Contains("market") || 
                cat.Contains("ulaşım") || cat.Contains("benzin") || cat.Contains("sağlık") || 
                cat.Contains("eğitim") || cat.Contains("aidat") || cat.Contains("gıda"))
            {
                return BudgetType.Needs;
            }

            // 2. BİRİKİMLER (%20)
            // Not: Transaction Type'ı 'Income' olanlar buraya girmez, 'Expense' olup yatırım amaçlı çıkanlar (örn: Altın Alımı) girebilir.
            // Ancak genelde birikim, gelirden kalandır. Biz harcama bazlı bakarsak:
            if (cat.Contains("yatırım") || cat.Contains("altın") || cat.Contains("döviz") || 
                cat.Contains("bireysel emeklilik") || cat.Contains("borç") || cat.Contains("kredi"))
            {
                return BudgetType.Savings;
            }

            // 3. İSTEKLER (%30) - Geriye kalan çoğu şey
            if (cat.Contains("eğlence") || cat.Contains("yemek") || cat.Contains("cafe") || 
                cat.Contains("restoran") || cat.Contains("giyim") || cat.Contains("teknoloji") || 
                cat.Contains("tatil") || cat.Contains("hobi") || cat.Contains("oyun") || cat.Contains("spotify") || cat.Contains("netflix"))
            {
                return BudgetType.Wants;
            }

            // Varsayılan olarak İSTEK sayalım (Bütçeyi zorlayanlar genelde bunlardır)
            return BudgetType.Wants;
        }
    }
}