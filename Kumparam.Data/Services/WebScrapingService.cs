using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using HtmlAgilityPack;
using Kumparam.Core.Interfaces;

namespace Kumparam.Data.Services
{
    public class WebScrapingService : IFinancialDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IUserRepository _userRepository;

        public WebScrapingService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            
            _httpClient = new HttpClient();
            // Header ayarları (Admin panelindeki gibi güçlü taklit)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        }

        public async Task<decimal> GetPriceAsync(string symbol, string sourceType = "Web")
        {
            if (string.IsNullOrWhiteSpace(symbol)) return 0;

            var configs = _userRepository.GetAllScrapingConfigs();
            if (configs == null) return 0;

            // Hem Sembolü hem de Kaynağı (Ziraat/Web) kontrol et
            var config = configs.FirstOrDefault(c => 
                !string.IsNullOrEmpty(c.Symbol) && 
                c.Symbol.Equals(symbol.Trim(), StringComparison.OrdinalIgnoreCase) &&
                c.SourceType == sourceType);

            if (config == null || !config.IsActive) return 0;

            string dbSymbol = config.Symbol.Trim(); 
            string finalUrl = config.TargetUrl?.Replace("[CODE]", dbSymbol).Replace("[LINK-ADI]", dbSymbol) ?? "";
            string finalXPath = config.HtmlPath_Selling?.Replace("[CODE]", dbSymbol).Replace("[LINK-ADI]", dbSymbol) ?? "";

            return await ScrapeData(finalUrl, finalXPath);
        }

        public async Task<decimal> GetBuyingPriceAsync(string symbol, string sourceType = "Web")
        {
            if (string.IsNullOrWhiteSpace(symbol)) return 0;

            var configs = _userRepository.GetAllScrapingConfigs();
            if (configs == null) return 0;

            var config = configs.FirstOrDefault(c => 
                !string.IsNullOrEmpty(c.Symbol) && 
                c.Symbol.Equals(symbol.Trim(), StringComparison.OrdinalIgnoreCase) &&
                c.SourceType == sourceType);

            if (config == null || !config.IsActive) return 0;

            string dbSymbol = config.Symbol.Trim();
            string finalUrl = config.TargetUrl?.Replace("[CODE]", dbSymbol).Replace("[LINK-ADI]", dbSymbol) ?? "";

            string rawXPath = !string.IsNullOrWhiteSpace(config.HtmlPath_Buying) 
                ? config.HtmlPath_Buying 
                : config.HtmlPath_Selling;

            string finalXPath = rawXPath?.Replace("[CODE]", dbSymbol).Replace("[LINK-ADI]", dbSymbol) ?? "";

            return await ScrapeData(finalUrl, finalXPath);
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
                    string rawText = priceNode.InnerText;
                    string cleanText = new string(rawText.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
                    cleanText = cleanText.Replace(".", "");
                    
                    var culture = new CultureInfo("tr-TR");
                    if (decimal.TryParse(cleanText, NumberStyles.Any, culture, out decimal price))
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