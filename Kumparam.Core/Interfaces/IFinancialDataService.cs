namespace Kumparam.Core.Interfaces;

public interface IFinancialDataService
{
    // Sembol verip (örn: "USD") güncel fiyatı (34.50) alacağız.
    // İnternet işlemi olduğu için "Task" (Asenkron) kullanıyoruz.
    Task<decimal> GetPriceAsync(string symbol);
    Task<decimal> GetBuyingPriceAsync(string symbol);
}