namespace Kumparam.Core.Interfaces;

public interface IFinancialDataService
{
    // Sembol verip (örn: "USD") güncel fiyatı (34.50) alacağız.
    // İnternet işlemi olduğu için "Task" (Asenkron) kullanıyoruz.
    Task<decimal> GetPriceAsync(string symbol, string sourceType = "Web");
    Task<decimal> GetBuyingPriceAsync(string symbol, string sourceType = "Web");
}