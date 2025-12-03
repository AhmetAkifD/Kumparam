// WebScrapingService.cs GÜNCEL HALİ:

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
        private readonly IUserRepository _userRepository; // YENİ: Veritabanı erişimi

        // Constructor değişti: Artık repository istiyor
        public WebScrapingService(IUserRepository userRepository)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            _userRepository = userRepository;
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            // 1. Veritabanından Ayarları Çek (DİNAMİK)
            var config = _userRepository.GetScrapingConfig(symbol);

            // Eğer veritabanında ayar yoksa veya pasifse 0 dön
            if (config == null || !config.IsActive) return 0;

            try
            {
                // 2. Ayarlardaki URL'ye git
                var html = await _httpClient.GetStringAsync(config.TargetUrl);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // 3. Ayarlardaki XPath ile veriyi bul
                var priceNode = htmlDoc.DocumentNode.SelectSingleNode(config.HtmlPath);
                
                if (priceNode != null)
                {
                    string priceText = priceNode.InnerText.Trim();
                    
                    // Temizlik
                    priceText = priceText.Replace("TL", "").Replace("$", "").Replace("%", "").Trim();

                    var culture = new CultureInfo("tr-TR");
                    if (decimal.TryParse(priceText, NumberStyles.Any, culture, out decimal price))
                    {
                        return price;
                    }
                }
            }
            catch (Exception)
            {
                return 0;
            }

            return 0;
        }

        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
            // Şimdilik aynı mantık
            return await GetPriceAsync(symbol);
        }
    }
}