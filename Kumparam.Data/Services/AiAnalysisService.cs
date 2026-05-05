using Kumparam.Core;
using Kumparam.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Transaction = Kumparam.Core.Transaction;

namespace Kumparam.Data.Services
{
    public class AiAnalysisService
    {
        private const string API_KEY = "AIzaSyA7yA-T1naBq7h2eVTYYYS7Oxqe5IYO23s";

        // DOĞRU VE AKTİF ENDPOINT (1.5 Flash):
        private const string ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public async Task<string> GenerateFinancialAdviceAsync(
            List<Transaction> transactions,
            List<Investment> investments,
            List<Goal> goals)
        {
            try
            {
                decimal totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                var topExpenseCategory = transactions.Where(t => t.Type == "Expense")
                                                     .GroupBy(t => t.Category)
                                                     .OrderByDescending(g => g.Sum(t => t.Amount))
                                                     .FirstOrDefault()?.Key ?? "Bilinmiyor";

                int activeGoalsCount = goals.Count(g => g.CurrentAmount < g.TargetAmount);
                decimal? totalInvestments = investments.Sum(i => i.Quantity * i.CurrentPrice);

                string prompt = $@"
Lütfen aşağıdaki finansal verilere sahip bir kullanıcı için 2-3 paragraflık, motivasyon verici, profesyonel ama samimi bir Türkçe finansal analiz ve tavsiye yazısı oluştur. PDF raporuna eklenecek.

**Kullanıcı Verileri:**
- Toplam Gelir: {totalIncome:N0} TL
- Toplam Gider: {totalExpense:N0} TL
- En Çok Harcama Yapılan Kategori: {topExpenseCategory}
- Devam Eden Hedef Sayısı: {activeGoalsCount}
- Toplam Yatırım Değeri (Yaklaşık): {totalInvestments:N0} TL

**Kurallar:**
1. Kesinlikle '*' (yıldız) veya '#' gibi markdown işaretleri kullanma. Sadece düz metin kullan.
2. Sayıları formatlı (örn: 10.000 TL) yaz.
3. Maddeleme veya liste kullanma, düz ve akıcı paragraflar olsun.
4. 'Kumparam Finansal Yapay Zeka Asistanı' olarak bir kapanış cümlesi ekle.";

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                // Türkçe karakterlerin (ş, ğ, ç) JSON içinde bozulmasını engelliyoruz
                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string jsonBody = JsonSerializer.Serialize(requestBody, jsonOptions);

                using var client = new HttpClient();

                // Google bizi bot sanıp engellemesin diye sahte bir tarayıcı/uygulama kimliği veriyoruz
                client.DefaultRequestHeaders.Add("User-Agent", "KumparamApp/1.0");

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                int maxRetries = 3;
                int delayDelayMs = 2000;

                for (int i = 0; i < maxRetries; i++)
                {
                    var response = await client.PostAsync($"{ENDPOINT}?key={API_KEY}", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();
                        using var document = JsonDocument.Parse(responseJson);

                        var textResponse = document.RootElement
                                                   .GetProperty("candidates")[0]
                                                   .GetProperty("content")
                                                   .GetProperty("parts")[0]
                                                   .GetProperty("text")
                                                   .GetString();

                        return textResponse ?? "Yapay zeka analizi oluşturulamadı.";
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (i == maxRetries - 1)
                        {
                            return "Yapay zeka sunucuları şu an çok yoğun olduğu için analiz eklenemedi. Lütfen daha sonra tekrar deneyin.";
                        }

                        await Task.Delay(delayDelayMs);
                        delayDelayMs *= 2;
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        return $"API Hatası ({(int)response.StatusCode}):\n{errorContent}";
                    }
                }

                return "Bilinmeyen bir hata oluştu.";
            }
            catch (Exception ex)
            {
                return $"Sistemsel Hata: {ex.Message}";
            }
        }
    }
}