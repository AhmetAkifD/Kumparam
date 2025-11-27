using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
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
            // Tarayıcı taklidi yapalım ki site bizi engellemesin
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            string url = GetUrlFromSymbol(symbol);
            if (string.IsNullOrEmpty(url)) return 0;

            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Doviz.com Fiyat Alanı (Genelde 'text-xl' veya 'data-socket-key' içinde olur)
                // Hisseler ve Altın için genelde şu yapı çalışır:
                var priceNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'text-xl') and contains(@class, 'font-semibold')]");
                
                // Eğer yukarıdaki bulamazsa alternatif (Hisse detay sayfaları için):
                if (priceNode == null)
                {
                    priceNode = htmlDoc.DocumentNode.SelectSingleNode("//span[contains(@class, 'value')]");
                }

                if (priceNode != null)
                {
                    string priceText = priceNode.InnerText.Trim();
                    
                    // Temizlik: "TL", "$", "%" ve boşlukları at
                    priceText = priceText.Replace("TL", "").Replace("$", "").Replace("%", "").Trim();

                    // Türk kültürüne göre (virgül ondalık) parse et
                    var culture = new CultureInfo("tr-TR");
                    if (decimal.TryParse(priceText, NumberStyles.Any, culture, out decimal price))
                    {
                        return price;
                    }
                }
            }
            catch (Exception)
            {
                return 0; // Çekilemezse 0 dön
            }

            return 0;
        }

        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
            // Şimdilik Satış fiyatı ile aynı (Makas farkı eklemek istersen burayı özelleştirebilirsin)
            return await GetPriceAsync(symbol);
        }

        private string GetUrlFromSymbol(string symbol)
        {
            // URL Yönlendirme Mantığı
            return symbol switch
            {
                // ALTINLAR (altin.doviz.com)
                "GLD" => "https://altin.doviz.com/gram-altin",
                "QGLD" => "https://altin.doviz.com/ceyrek-altin",
                
                // KRİPTOLAR (borsa.doviz.com veya ana site)
                "BTC" => "https://www.doviz.com/kripto-paralar/bitcoin",
                "ETH" => "https://www.doviz.com/kripto-paralar/ethereum",
                
                // HİSSE SENETLERİ (borsa.doviz.com/hisseler)
                "THYAO" => "https://borsa.doviz.com/hisseler/thyao-turk-hava-yollari",
                "ASELS" => "https://borsa.doviz.com/hisseler/asels-aselsan",
                "GARAN" => "https://borsa.doviz.com/hisseler/garan-garanti-bankasi",
                "SISE"  => "https://borsa.doviz.com/hisseler/sise-sise-cam",
                "KCHOL" => "https://borsa.doviz.com/hisseler/kchol-koc-holding",
                
                _ => "" // Bilinmeyen sembol
            };
        }
    }
}