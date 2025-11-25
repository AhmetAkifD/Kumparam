namespace Kumparam.Core
{
    // Bu bizim "Sözleşmemiz" (Repository Pattern)
    // Data katmanının NELER YAPABİLECEĞİNİ söyler.
    public interface IUserRepository
    {
        /// <summary>
        /// Verilen e-postaya sahip bir kullanıcıyı getirir.
        /// </summary>
        /// <param name="email">Aranacak e-posta</param>
        /// <returns>Kullanıcı bulunduysa User nesnesi, bulunamadıysa null</returns>
        User? GetUserByEmail(string email);

        /// <summary>
        /// Bu e-posta adresinin veritabanında zaten var olup olmadığını kontrol eder.
        /// </summary>
        bool EmailExists(string email);

        /// <summary>
        /// Yeni bir kullanıcı ve profilini veritabanına ekler.
        /// </summary>
        void AddUser(User user, UserProfile profile);

        // YENİ: Bağlantı testi için
        bool IsConnectionSuccess(); 
        
        // YENİ: Kullanıcının finansal özetini getirir
        public FinancialSummary GetFinancialSummary(Guid userId)
        {
            // Şimdilik veritabanında işlem tablosu olmadığı için
            // SAHTE VERİ (Dummy Data) döndürüyoruz.
            // İleride buraya gerçek SQL sorgusu gelecek.
            
            return new FinancialSummary
            {
                TotalBalance = 12500.50m,       // Örnek: 12.500 TL
                MonthlyIncome = 4500.00m,       // Örnek: 4.500 TL
                MonthlyExpense = 1200.00m,      // Örnek: 1.200 TL
                SavingsGoalProgress = 65        // Örnek: %65
            };
        }
        void AddTransaction(Transaction transaction);
        List<Transaction> GetLastTransactions(Guid userId, int count);
        List<ExpenseStat> GetExpenseStats(Guid userId);
    }
}