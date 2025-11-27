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

        // Alım İşlemi İçin (Bankanın Satış Kuru)
        public async Task<decimal> GetPriceAsync(string symbol)
        {
            // Kısıtlamayı kaldırdık! Artık TCMB'de olan her şeyi çeker.
            // Altın ve kripto gibi TCMB'de olmayanlar zaten XML'de bulunamayacağı için 0 döner.
            
            try
            {
                using (var stream = await _httpClient.GetStreamAsync(TcmbUrl))
                {
                    var xDoc = XDocument.Load(stream);

                    var currencyNode = xDoc.Descendants("Currency")
                        .FirstOrDefault(x => x.Attribute("CurrencyCode")?.Value == symbol);

                    if (currencyNode != null)
                    {
                        // Biz alırken banka satar -> ForexSelling
                        string priceText = currencyNode.Element("ForexSelling")?.Value;
                        
                        if (string.IsNullOrEmpty(priceText))
                            priceText = currencyNode.Element("BanknoteSelling")?.Value;

                        if (!string.IsNullOrEmpty(priceText) && 
                            decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                        {
                            return price;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TCMB Hatası: " + ex.Message);
            }

            return 0;
        }

        // Satış İşlemi İçin (Bankanın Alış Kuru)
        public async Task<decimal> GetBuyingPriceAsync(string symbol)
        {
             // Kısıtlamayı kaldırdık!
            
            try
            {
                using (var stream = await _httpClient.GetStreamAsync(TcmbUrl))
                {
                    var xDoc = XDocument.Load(stream);

                    var currencyNode = xDoc.Descendants("Currency")
                        .FirstOrDefault(x => x.Attribute("CurrencyCode")?.Value == symbol);

                    if (currencyNode != null)
                    {
                        // Biz satarken banka alır -> ForexBuying
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