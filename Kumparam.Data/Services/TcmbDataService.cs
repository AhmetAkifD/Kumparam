using System.Globalization;
using System.Net.Http;
using System.Xml.Linq;
using Kumparam.Core.Interfaces;

namespace Kumparam.Data.Services
{
    public class TcmbDataService : IFinancialDataService
    {
        private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            // TCMB sadece dövizleri destekler (USD, EUR, GBP vs.)
            // Altın (GLD) veya Kripto (BTC) sorulursa 0 dön veya Scraping servisine yönlendir
            if (symbol == "GLD" || symbol == "QGLD" || symbol == "BTC" || symbol == "ETH" || symbol == "XU100")
            {
                return 0; // Şimdilik desteklenmiyor (veya eski Scraping kodunu buraya yedek olarak ekleyebilirsin)
            }

            try
            {
                using (var client = new HttpClient())
                {
                    // 1. XML Verisini İndir
                    var xmlStream = await client.GetStreamAsync(TcmbUrl);
                    
                    // 2. XML'i Oku
                    var xDoc = XDocument.Load(xmlStream);

                    // 3. İlgili Para Birimini Bul (CurrencyCode="USD" gibi)
                    var currencyNode = xDoc.Descendants("Currency")
                        .FirstOrDefault(x => x.Attribute("CurrencyCode")?.Value == symbol);

                    if (currencyNode != null)
                    {
                        // 4. ForexBuying (Döviz Alış) değerini al
                        // TCMB'de ondalık ayracı nokta (.)'dır.
                        string priceText = currencyNode.Element("ForexBuying")?.Value;
                        
                        if (!string.IsNullOrEmpty(priceText) && 
                            decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                        {
                            return price;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Bağlantı hatası vs. olursa 0 dön
                return 0;
            }

            return 0;
        }
    }
}