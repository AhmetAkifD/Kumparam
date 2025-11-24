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
    }
}