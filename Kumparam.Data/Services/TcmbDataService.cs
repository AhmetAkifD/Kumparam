using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kumparam.Core.Interfaces;

namespace Kumparam.Data.Services
{
    public class TcmbDataService : IFinancialDataService
    {
        private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";
        private readonly HttpClient _httpClient;

        public TcmbDataService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            // Sadece USD ve EUR destekliyoruz
            if (symbol != "USD" && symbol != "EUR") return 0;

            try
            {
                var xmlStream = await _httpClient.GetStreamAsync(TcmbUrl);
                var xDoc = XDocument.Load(xmlStream);

                var currencyNode = xDoc.Descendants("Currency")
                    .FirstOrDefault(x => x.Attribute("CurrencyCode")?.Value == symbol);

                if (currencyNode != null)
                {
                    // DÜZELTME BURADA: "ForexBuying" yerine "ForexSelling" (Satış Kuru)
                    string priceText = currencyNode.Element("ForexSelling")?.Value;
                    
                    // Eğer ForexSelling boşsa (bazen olabilir), BanknoteSelling'i dene
                    if (string.IsNullOrEmpty(priceText))
                    {
                        priceText = currencyNode.Element("BanknoteSelling")?.Value;
                    }

                    if (!string.IsNullOrEmpty(priceText) && 
                        decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                    {
                        return price;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TCMB Hatası: " + ex.Message);
            }

            return 0;
        }
        // TcmbDataService.cs içine ekle:

        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
            if (symbol != "USD" && symbol != "EUR") return 0;

            try
            {
                // XML mantığı aynı, sadece çektiğimiz alan farklı
                using (var client = new HttpClient())
                {
                    var xmlStream = await client.GetStreamAsync("https://www.tcmb.gov.tr/kurlar/today.xml");
                    var xDoc = XDocument.Load(xmlStream);

                    var currencyNode = System.Linq.Enumerable.FirstOrDefault(xDoc.Descendants("Currency"), x => x.Attribute("CurrencyCode")?.Value == symbol);

                    if (currencyNode != null)
                    {
                        // BURASI FARKLI: "ForexBuying" (Alış Kuru) çekiyoruz
                        string priceText = currencyNode.Element("ForexBuying")?.Value;
                
                        if (string.IsNullOrEmpty(priceText))
                            priceText = currencyNode.Element("BanknoteBuying")?.Value;

                        if (!string.IsNullOrEmpty(priceText) && 
                            decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                        {
                            return price;
                        }
                    }
                }
            }
            catch (Exception) { return 0; }
            return 0;
        }
    }
}