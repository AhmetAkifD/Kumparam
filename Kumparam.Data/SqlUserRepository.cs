using Kumparam.Core;
using Microsoft.Data.SqlClient; // NuGet'ten eklediğimiz paket

namespace Kumparam.Data
{
    // Bu sınıf, IUserRepository sözleşmesini ADO.NET (SQL) kullanarak uygular.
    public class SqlUserRepository : IUserRepository
    {
        // Connection string'i burada saklayacağız
        private readonly string _connectionString;

        // Constructor (Yapıcı Metot):
        // Bu sınıf çağrıldığında (new SqlUserRepository) connection string'i almamız lazım
        public SqlUserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        public bool IsConnectionSuccess()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public User? GetUserByEmail(string email)
        {
            User? user = null;

            // 'using' blokları, bağlantının ve komutun işi bitince otomatik kapanmasını sağlar
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT UserId, Email, PasswordHash, PasswordSalt FROM Users WHERE Email = @Email";
                using (var command = new SqlCommand(sql, connection))
                {
                    // SQL Injection'a karşı parametre kullanmak ZORUNLUDUR
                    command.Parameters.AddWithValue("@Email", email);
                    
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new User
                            {
                                UserId = (Guid)reader["UserId"],
                                Email = (string)reader["Email"],
                                PasswordHash = (byte[])reader["PasswordHash"],
                                PasswordSalt = (byte[])reader["PasswordSalt"]
                            };
                        }
                    }
                }
            }
            return user;
        }

        public bool EmailExists(string email)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT 1 FROM Users WHERE Email = @Email";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    
                    connection.Open();
                    // ExecuteScalar, tek bir değer (veya null) döndürür
                    var result = command.ExecuteScalar(); 
                    return result != null;
                }
            }
        }

        public void AddUser(User user, UserProfile profile)
        {
            // İki tabloya (Users ve UserProfiles) yazma işlemi yapacağımız için,
            // ikisinden biri başarısız olursa hepsini geri almak (Rollback) için
            // bir "Transaction" (İşlem) başlatmamız şarttır.
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. ADIM: Users tablosuna ekle
                        var userSql = "INSERT INTO Users (UserId, Email, PasswordHash, PasswordSalt) " +
                                      "VALUES (@UserId, @Email, @PasswordHash, @PasswordSalt)";
                        
                        using (var userCommand = new SqlCommand(userSql, connection, transaction))
                        {
                            userCommand.Parameters.AddWithValue("@UserId", user.UserId);
                            userCommand.Parameters.AddWithValue("@Email", user.Email);
                            // HATA BURADAYDI: @PasswordRoom -> @PasswordHash olarak düzeltildi
                            userCommand.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
                            userCommand.Parameters.AddWithValue("@PasswordSalt", user.PasswordSalt);
                            
                            userCommand.ExecuteNonQuery(); // Sorguyu çalıştır
                        }

                        // 2. ADIM: UserProfiles tablosuna ekle
                        var profileSql = "INSERT INTO UserProfiles (UserId, FirstName, LastName) " +
                                         "VALUES (@UserId, @FirstName, @LastName)";
                        
                        using (var profileCommand = new SqlCommand(profileSql, connection, transaction))
                        {
                            profileCommand.Parameters.AddWithValue("@UserId", profile.UserId);
                            profileCommand.Parameters.AddWithValue("@FirstName", profile.FirstName);
                            profileCommand.Parameters.AddWithValue("@LastName", profile.LastName);
                            
                            profileCommand.ExecuteNonQuery(); // Sorguyu çalıştır
                        }

                        // Her iki sorgu da başarılıysa, işlemi onayla (Commit)
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        // Eğer bir hata olursa (örn: Email unique hatası), tüm işlemi geri al (Rollback)
                        transaction.Rollback();
                        throw; // Hatayı tekrar fırlat ki UI katmanı haberdar olsun
                    }
                }
            }
        }
    }
}