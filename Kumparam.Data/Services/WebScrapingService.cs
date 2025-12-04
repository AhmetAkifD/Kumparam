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
            var config = _userRepository.GetScrapingConfig(symbol);
            if (config == null || !config.IsActive) return 0;

            // SATIŞ İÇİN: HtmlPath_Selling kullan
            return await ScrapeData(config.TargetUrl, config.HtmlPath_Selling);
        }

        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
            var config = _userRepository.GetScrapingConfig(symbol);
            if (config == null || !config.IsActive) return 0;

            // ALIŞ İÇİN: HtmlPath_Buying kullan
            return await ScrapeData(config.TargetUrl, config.HtmlPath_Buying);
        }
        private async Task<decimal> ScrapeData(string url, string xpath)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var priceNode = htmlDoc.DocumentNode.SelectSingleNode(xpath);
        
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
            catch
            {
                return 0;
            }
            return 0;
        }
    }
}