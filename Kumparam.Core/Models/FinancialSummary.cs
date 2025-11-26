namespace Kumparam.Core;

public class FinancialSummary
{
    public decimal TotalBalance { get; set; }      // Toplam Bakiye
    public decimal MonthlyIncome { get; set; }     // Bu Ay Gelir
    public decimal MonthlyExpense { get; set; }    // Bu Ay Gider
    public decimal SavingsGoalProgress { get; set; } // Tasarruf Hedefi (%)
}