using Kumparam.Core.Models;

namespace Kumparam.Core.Interfaces
{
    public interface IUserRepository
    {
        User? GetUserByEmail(string email);
        bool EmailExists(string email);
        void AddUser(User user, UserProfile profile);
        bool IsConnectionSuccess(); 
        public FinancialSummary GetFinancialSummary(Guid userId)
        {
            return new FinancialSummary
            {
                TotalBalance = 12500.50m,
                MonthlyIncome = 4500.00m,
                MonthlyExpense = 1200.00m,
                SavingsGoalProgress = 65
            };
        }
        void AddTransaction(Transaction transaction);
        List<Transaction> GetLastTransactions(Guid userId, int count);
        List<ExpenseStat> GetExpenseStats(Guid userId);
        void AddGoal(Goal goal);
        List<Goal> GetGoals(Guid userId);
        void DeleteGoal(Guid goalId);
        void UpdateGoalAmount(Guid goalId, decimal amount);
        string? GetFirstGoalTitle(Guid userId);
        void AddInvestment(Investment investment);
        List<Investment> GetInvestments(Guid userId);
        void DeleteInvestment(Guid investmentId);
        void UpdateInvestmentQuantity(Guid investmentId, decimal newQuantity);
    }
}