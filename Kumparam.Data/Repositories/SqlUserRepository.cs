using Kumparam.Core;
using Kumparam.Core.Interfaces;
using Kumparam.Core.Models;
using Microsoft.Data.SqlClient;

// NuGet'ten eklediğimiz paket

namespace Kumparam.Data.Repositories
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
        // SqlUserRepository.cs dosyasının içine, diğer metotların yanına ekle/güncelle:

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
        // MEVCUT GetFinancialSummary METODUNU BUNUNLA DEĞİŞTİR:
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

                // 4. HEDEF (YENİLENDİ: Artık Goals Tablosundan Hesaplanıyor!)
                // Tüm hedeflerin toplam tutarını ve şu anki birikimini çekiyoruz
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
        // SqlUserRepository.cs içine ekle:
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
        // Kumparam.Data/SqlUserRepository.cs içine:

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
                var sql = @"INSERT INTO Investments (UserId, Name, Symbol, Quantity, BuyingPrice, CurrentPrice, PurchaseDate) 
                            VALUES (@UserId, @Name, @Symbol, @Quantity, @BuyingPrice, @CurrentPrice, @PurchaseDate)";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@UserId", investment.UserId);
                    cmd.Parameters.AddWithValue("@Name", investment.Name);
                    cmd.Parameters.AddWithValue("@Symbol", investment.Symbol ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Quantity", investment.Quantity);
                    cmd.Parameters.AddWithValue("@BuyingPrice", investment.BuyingPrice);
                    cmd.Parameters.AddWithValue("@CurrentPrice", investment.CurrentPrice ?? (object)DBNull.Value);
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
                                CurrentPrice = reader["CurrentPrice"] == DBNull.Value ? null : (decimal?)reader["CurrentPrice"],
                                PurchaseDate = (DateTime)reader["PurchaseDate"]
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
    }
}