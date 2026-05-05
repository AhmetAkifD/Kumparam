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
        UserProfile GetUserProfile(Guid userId);
        void UpdateUserProfile(UserProfile profile);
        void ResetUserData(Guid userId);
        void UpdatePassword(Guid userId, byte[] passwordHash, byte[] passwordSalt);
        User? GetUserById(Guid userId);
        List<ScrapingConfig> GetAllScrapingConfigs();
        ScrapingConfig? GetScrapingConfig(string symbol);
        void AddScrapingConfig(ScrapingConfig config);
        void UpdateScrapingConfig(ScrapingConfig config);
        void DeleteScrapingConfig(int configId);
        List<Transaction> GetAllTransactions(Guid userId);
        void DeleteTransaction(Guid transactionId);
        List<Transaction> GetDeletedTransactions(Guid userId);
        void RestoreTransaction(int deletedId);
        void PermanentlyDeleteTransaction(int deletedId);
        void DeleteUser(Guid currentUserId);
        List<string> GetCategories();
        // Otomatik İşlemler (AutoTransactions)
        void AddAutoTransaction(AutoTransaction autoTrans);
        List<AutoTransaction> GetAutoTransactions(Guid userId);
        void DeleteAutoTransaction(Guid autoId);
        void UpdateAutoTransactionNextRun(Guid autoId, DateTime nextRunDate);
        // Hedef Otomasyonu (GoalAutomations)
        void AddGoalAutomation(GoalAutomation automation);
        List<GoalAutomation> GetGoalAutomations(Guid userId);
        void DeleteGoalAutomation(Guid automationId);
        void UpdateGoalAutomationNextRun(Guid automationId, DateTime nextRunDate);
    }
}