using System.Security.Cryptography;
using System.Text;

namespace Kumparam.Core
{
    // Bu sınıf, şifreleri güvenli bir şekilde hash'lemek ve doğrulamak için
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bit
        private const int HashSize = 64; // 512 bit
        private const int Iterations = 350000; // PBKDF2 için iterasyon sayısı
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA512;

        /// <summary>
        /// Yeni bir şifre için Salt ve Hash oluşturur.
        /// </summary>
        /// <param name="password">Kullanıcının girdiği düz şifre</param>
        /// <param name="salt">Oluşturulan rastgele salt (DB'ye kaydedilecek)</param>
        /// <param name="hash">Oluşturulan hash (DB'ye kaydedilecek)</param>
        public static void HashPassword(string password, out byte[] salt, out byte[] hash)
        {
            salt = RandomNumberGenerator.GetBytes(SaltSize);
            
            hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                HashSize
            );
        }

        /// <summary>
        /// Giriş denemesindeki şifrenin, veritabanındaki hash ile eşleşip eşleşmediğini kontrol eder.
        /// </summary>
        public static bool VerifyPassword(string password, byte[] salt, byte[] hash)
        {
            var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                HashSize
            );

            // İki byte dizisinin de aynı olup olmadığını güvenli bir şekilde kontrol et
            return hashToCompare.SequenceEqual(hash);
        }
    }
}