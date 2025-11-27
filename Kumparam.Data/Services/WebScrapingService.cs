using System.Globalization;
using System.Net.Http;
using System.Windows;
using HtmlAgilityPack;
using Kumparam.Core.Interfaces;

namespace Kumparam.Data.Services
{
    public class WebScrapingService : IFinancialDataService
    {
        private readonly HttpClient _httpClient;

        public WebScrapingService()
        {
            _httpClient = new HttpClient();
            // Bazı siteler robot olduğumuzu anlamasın diye tarayıcı gibi görünelim
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            
            // 1. Sembolü Siteye Uygun Hale Getir
            // Örn: USD -> "gram-altin" veya "bitcoin" gibi slug'lara çevirmemiz gerekebilir.
            // Şimdilik basit bir switch-case ile eşleştirelim.
            string slug = GetSlugFromSymbol(symbol);
            
            if (string.IsNullOrEmpty(slug)) return 0; // Tanımsız sembol

            string url = $"https://www.doviz.com/{slug}"; // Örn: https://www.doviz.com/altin/gram-altin    

            try
            {
                // 2. Sayfayı İndir
                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                if (html.Contains("captcha") || html.Contains("robot"))
                {
                    // Bunu bir yere loglayabilir veya breakpoint ile kontrol edebilirsin.
                   MessageBox.Show("Banlandınız"); // Engellendik işareti
                }
                // 3. Fiyatın Olduğu Yeri Bul (XPath)
                // Not: Bu XPath, sitenin tasarımına göre değişebilir.
                // Genelde fiyatlar "text-xl" veya data-socket-key gibi özelliklerde durur.
                // Doviz.com'da güncel fiyat genelde: <div class="text-xl font-semibold" ... >34,50</div>
                
                var priceNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'text-xl') and contains(@class, 'font-semibold')]");
                
                if (priceNode != null)
                {
                    // "34,50" veya "2.500,00" gibi metni temizle
                    string priceText = priceNode.InnerText.Trim();
                    
                    // Türk Lirası sembolünü veya gereksiz boşlukları sil
                    priceText = priceText.Replace("TL", "").Replace("$", "").Trim();

                    // Kültür farkını (virgül/nokta) hallet (TR kültürü: virgül ondalık)
                    var culture = new CultureInfo("tr-TR");
                    if (decimal.TryParse(priceText, NumberStyles.Any, culture, out decimal price))
                    {
                        return price;
                    }
                }
            }
            catch (Exception)
            {
                // İnternet yoksa veya site değiştiyse 0 dön (Uygulama patlamasın)
                return 0;
            }

            return 0;
        }

        private string GetSlugFromSymbol(string symbol)
        {
            return symbol switch
            {
                "USD" => "doviz/amerikan-dolari",
                "EUR" => "doviz/euro",
                "GBP" => "doviz/sterlin",
                "GLD" => "altin/gram-altin",
                "QGLD" => "altin/ceyrek-altin",
                "BTC" => "kripto-paralar/bitcoin",
                "ETH" => "kripto-paralar/ethereum",
                _ => "" // Bilinmeyen sembol
            };
        }
        // WebScrapingService.cs içine ekle:

        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
            // Şimdilik Satış fiyatı ile aynı mantığı kullanalım.
            // İleride buraya "Alış" fiyatını çeken özel XPath yazılabilir.
            // Örn: //div[contains(@text, 'Alış')]/span ...
    
            return await GetPriceAsync(symbol);
        }
    }
}