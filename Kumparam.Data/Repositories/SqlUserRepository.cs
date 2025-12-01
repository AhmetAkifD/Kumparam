using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Microsoft.Data.SqlClient;

namespace Kumparam.Data.Repositories
{
    public class SqlUserRepository : IUserRepository
    {
        private readonly string _connectionString;
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
        public void AddTransaction(Transaction transaction)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"INSERT INTO Transactions (UserId, Amount, Type, Category, Description, TransactionDate) 
                            VALUES (@UserId, @Amount, @Type, @Category, @Description, @TransactionDate)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@UserId", transaction.UserId);
                    command.Parameters.AddWithValue("@Amount", transaction.Amount);
                    command.Parameters.AddWithValue("@Type", transaction.Type);
                    command.Parameters.AddWithValue("@Category", transaction.Category ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Description", transaction.Description ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@TransactionDate", transaction.TransactionDate);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }
        public FinancialSummary GetFinancialSummary(Guid userId)
        {
            var summary = new FinancialSummary();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // 1. Gelir (Değişmedi)
                var sqlIncome = "SELECT ISNULL(SUM(Amount), 0) FROM Transactions WHERE UserId = @UserId AND Type = 'Income'";
                using (var cmd = new SqlCommand(sqlIncome, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    summary.MonthlyIncome = (decimal)cmd.ExecuteScalar();
                }

                // 2. Gider (Değişmedi)
                var sqlExpense = "SELECT ISNULL(SUM(Amount), 0) FROM Transactions WHERE UserId = @UserId AND Type = 'Expense'";
                using (var cmd = new SqlCommand(sqlExpense, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    summary.MonthlyExpense = (decimal)cmd.ExecuteScalar();
                }

                // 3. Bakiye (Değişmedi)
                summary.TotalBalance = summary.MonthlyIncome - summary.MonthlyExpense;
                
                var sqlGoals = "SELECT SUM(TargetAmount), SUM(CurrentAmount) FROM Goals WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(sqlGoals, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && reader[0] != DBNull.Value)
                        {
                            decimal totalTarget = (decimal)reader[0];  // Toplam Hedeflenen
                            decimal totalCurrent = (decimal)reader[1]; // Toplam Birikmiş

                            if (totalTarget > 0)
                            {
                                // Genel İlerleme Yüzdesi
                                summary.SavingsGoalProgress = (totalCurrent / totalTarget) * 100;
                                
                                // %100'ü geçmesin
                                if (summary.SavingsGoalProgress > 100) summary.SavingsGoalProgress = 100;
                            }
                        }
                        else
                        {
                            // Hiç hedef yoksa 0
                            summary.SavingsGoalProgress = 0;
                        }
                    }
                }
            }
            return summary;
        }
        public List<Transaction> GetLastTransactions(Guid userId, int count)
        {
            var list = new List<Transaction>();
    
            using (var connection = new SqlConnection(_connectionString))
            {
                // "TOP @Count" ile sadece istediğimiz kadarını (örn: 10 tane) çekiyoruz.
                // "ORDER BY TransactionDate DESC" ile en yeniden en eskiye sıralıyoruz.
                var sql = @"SELECT TOP (@Count) * FROM Transactions 
                    WHERE UserId = @UserId 
                    ORDER BY TransactionDate DESC";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Count", count);
            
                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Transaction
                            {
                                TransactionId = (Guid)reader["TransactionId"],
                                UserId = (Guid)reader["UserId"],
                                Amount = (decimal)reader["Amount"],
                                Type = (string)reader["Type"],
                                Category = reader["Category"] != DBNull.Value ? (string)reader["Category"] : "",
                                Description = reader["Description"] != DBNull.Value ? (string)reader["Description"] : "",
                                TransactionDate = (DateTime)reader["TransactionDate"]
                            });
                        }
                    }
                }
            }
            return list;
        }
        public List<ExpenseStat> GetExpenseStats(Guid userId)
        {
            var list = new List<ExpenseStat>();
    
            using (var connection = new SqlConnection(_connectionString))
            {
                // Sadece 'Expense' (Gider) olanları kategoriye göre gruplayıp topluyoruz
                var sql = @"SELECT Category, SUM(Amount) as TotalAmount 
                    FROM Transactions 
                    WHERE UserId = @UserId AND Type = 'Expense' 
                    GROUP BY Category";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
            
                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ExpenseStat
                            {
                                Category = reader["Category"] != DBNull.Value ? (string)reader["Category"] : "Diğer",
                                TotalAmount = (decimal)reader["TotalAmount"]
                            });
                        }
                    }
                }
            }
            return list;
        }
        public void AddGoal(Goal goal)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"INSERT INTO Goals (UserId, Title, TargetAmount, CurrentAmount, Deadline, Description) 
                            VALUES (@UserId, @Title, @TargetAmount, @CurrentAmount, @Deadline, @Description)";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", goal.UserId);
                    cmd.Parameters.AddWithValue("@Title", goal.Title);
                    cmd.Parameters.AddWithValue("@TargetAmount", goal.TargetAmount);
                    cmd.Parameters.AddWithValue("@CurrentAmount", goal.CurrentAmount);
                    cmd.Parameters.AddWithValue("@Deadline", goal.Deadline ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", goal.Description ?? (object)DBNull.Value);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public List<Goal> GetGoals(Guid userId)
        {
            var list = new List<Goal>();
            using (var connection = new SqlConnection(_connectionString))
            {
                // Tüm sütunları isimleriyle çağırıyoruz
                var sql = @"SELECT GoalId, UserId, Title, TargetAmount, CurrentAmount, Deadline, Description 
                    FROM Goals 
                    WHERE UserId = @UserId 
                    ORDER BY CreatedAt DESC";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    connection.Open();
            
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Goal
                            {
                                // ID ve Başlık (Bunlar SQL'de NOT NULL olduğu için direkt alabiliriz)
                                GoalId = (Guid)reader["GoalId"],
                                UserId = (Guid)reader["UserId"],
                                Title = reader["Title"].ToString(),

                                // Para Birimleri (Convert.ToDecimal kullanımı en güvenlisidir)
                                TargetAmount = reader["TargetAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TargetAmount"]) : 0,
                                CurrentAmount = reader["CurrentAmount"] != DBNull.Value ? Convert.ToDecimal(reader["CurrentAmount"]) : 0,
                        
                                // Tarih ve Açıklama (NULL gelebilir, kontrol şart)
                                Deadline = reader["Deadline"] == DBNull.Value ? null : (DateTime?)reader["Deadline"],
                                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }
        public void DeleteGoal(Guid goalId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "DELETE FROM Goals WHERE GoalId = @GoalId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@GoalId", goalId);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public void UpdateGoalAmount(Guid goalId, decimal amount)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                // Mevcut tutarın üzerine ekleme yapıyoruz (amount negatif gelirse çıkarma olur)
                var sql = "UPDATE Goals SET CurrentAmount = CurrentAmount + @Amount WHERE GoalId = @GoalId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@GoalId", goalId);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public string? GetFirstGoalTitle(Guid userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                // Sadece Başlığı (Title) seçiyoruz, sadece 1 tane.
                var sql = "SELECT TOP 1 Title FROM Goals WHERE UserId = @UserId";
        
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    connection.Open();
            
                    // Tek bir değer döndürür (object tipinde)
                    var result = cmd.ExecuteScalar();
            
                    if (result != null)
                    {
                        return result.ToString();
                    }
            
                    return "Veritabanında Hedef Bulunamadı";
                }
            }
        }
        public void AddInvestment(Investment investment)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                // CurrentPrice ÇIKARILDI
                var sql = @"INSERT INTO Investments (UserId, Name, Symbol, Quantity, BuyingPrice, PurchaseDate) 
                            VALUES (@UserId, @Name, @Symbol, @Quantity, @BuyingPrice, @PurchaseDate)";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", investment.UserId);
                    cmd.Parameters.AddWithValue("@Name", investment.Name);
                    cmd.Parameters.AddWithValue("@Symbol", investment.Symbol ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Quantity", investment.Quantity);
                    cmd.Parameters.AddWithValue("@BuyingPrice", investment.BuyingPrice);
                    // CurrentPrice PARAMETRESİ SİLİNDİ
                    cmd.Parameters.AddWithValue("@PurchaseDate", investment.PurchaseDate);

                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public List<Investment> GetInvestments(Guid userId)
        {
            var list = new List<Investment>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM Investments WHERE UserId = @UserId ORDER BY PurchaseDate DESC";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Investment
                            {
                                InvestmentId = (Guid)reader["InvestmentId"],
                                UserId = (Guid)reader["UserId"],
                                Name = reader["Name"].ToString(),
                                Symbol = reader["Symbol"] as string,
                                Quantity = (decimal)reader["Quantity"],
                                BuyingPrice = (decimal)reader["BuyingPrice"],
                                PurchaseDate = (DateTime)reader["PurchaseDate"],
                                
                                // CurrentPrice ARTIK VERİTABANINDAN GELMİYOR!
                                // Varsayılan olarak 0 veya BuyingPrice atayabiliriz,
                                // ama asıl değer birazdan TCMB'den gelecek.
                                CurrentPrice = (decimal)reader["BuyingPrice"] 
                            });
                        }
                    }
                }
            }
            return list;
        }
        public void DeleteInvestment(Guid investmentId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "DELETE FROM Investments WHERE InvestmentId = @InvestmentId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@InvestmentId", investmentId);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public void UpdateInvestmentQuantity(Guid investmentId, decimal newQuantity)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE Investments SET Quantity = @Quantity WHERE InvestmentId = @InvestmentId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@Quantity", newQuantity);
                    cmd.Parameters.AddWithValue("@InvestmentId", investmentId);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public UserProfile GetUserProfile(Guid userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT FirstName, LastName FROM UserProfiles WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserProfile
                            {
                                UserId = userId,
                                FirstName = reader["FirstName"] != DBNull.Value ? (string)reader["FirstName"] : "",
                                LastName = reader["LastName"] != DBNull.Value ? (string)reader["LastName"] : ""
                            };
                        }
                    }
                }
            }
            // Eğer profil yoksa boş döndür
            return new UserProfile { UserId = userId };
        }
        public void UpdateUserProfile(UserProfile profile)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE UserProfiles SET FirstName = @FirstName, LastName = @LastName WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", profile.UserId);
                    cmd.Parameters.AddWithValue("@FirstName", profile.FirstName ?? "");
                    cmd.Parameters.AddWithValue("@LastName", profile.LastName ?? "");
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public void ResetUserData(Guid userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Yatırımları Sil
                        var sqlInvest = "DELETE FROM Investments WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(sqlInvest, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Hedefleri Sil
                        var sqlGoals = "DELETE FROM Goals WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(sqlGoals, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. İşlemleri (Gelir/Gider) Sil
                        var sqlTrans = "DELETE FROM Transactions WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(sqlTrans, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        // SqlUserRepository.cs içine ekle:

        public void UpdatePassword(Guid userId, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public User? GetUserById(Guid userId)
        {
            User? user = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT UserId, Email, PasswordHash, PasswordSalt FROM Users WHERE UserId = @UserId";
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
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
    }
}