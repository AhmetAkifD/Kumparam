using System;
using Microsoft.Data.SqlClient;

namespace Kumparam.Data.Helpers // Eğer klasör açmazsan ".Helpers" kısmını sil
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;

        public DatabaseInitializer(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Initialize()
        {
            // 1. ADIM: Veritabanının Kendisini Oluştur (Yoksa)
            CreateDatabaseIfNotExists();

            // 2. ADIM: Tabloları Oluştur (Yoksa)
            CreateTablesIfNotExist();
        }

        private void CreateDatabaseIfNotExists()
        {
            // Bağlantı cümlesini parçalarına ayırıyoruz
            var builder = new SqlConnectionStringBuilder(_connectionString);
            string targetDatabaseName = builder.InitialCatalog; // "KumparamDB" ismini aldık

            // Geçici olarak "master" (Yönetici) veritabanına bağlanacağız
            builder.InitialCatalog = "master"; 

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();

                // Veritabanı var mı kontrol et, yoksa OLUŞTUR
                var sql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{targetDatabaseName}')
                    BEGIN
                        CREATE DATABASE [{targetDatabaseName}];
                    END";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void CreateTablesIfNotExist()
        {
            // Artık kendi veritabanımıza (KumparamDB) bağlanabiliriz
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // --- 1. Users Tablosu ---
                var usersTable = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                    CREATE TABLE Users (
                        UserId UNIQUEIDENTIFIER PRIMARY KEY,
                        Email NVARCHAR(256) NOT NULL UNIQUE,
                        PasswordHash VARBINARY(64) NOT NULL,
                        PasswordSalt VARBINARY(16) NOT NULL,
                        CreatedAt DATETIME2 DEFAULT GETDATE()
                    );";
                ExecuteCommand(connection, usersTable);

                // --- 2. UserProfiles Tablosu ---
                var profilesTable = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserProfiles' AND xtype='U')
                    CREATE TABLE UserProfiles (
                        UserId UNIQUEIDENTIFIER PRIMARY KEY,
                        FirstName NVARCHAR(100),
                        LastName NVARCHAR(100),
                        CONSTRAINT FK_UserProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );";
                ExecuteCommand(connection, profilesTable);

                // --- 3. Transactions Tablosu ---
                var transactionsTable = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Transactions' AND xtype='U')
                    CREATE TABLE Transactions (
                        TransactionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        Amount DECIMAL(18, 2) NOT NULL,
                        Type NVARCHAR(20) NOT NULL,
                        Category NVARCHAR(50),
                        Description NVARCHAR(200),
                        TransactionDate DATETIME2 DEFAULT GETDATE(),
                        CONSTRAINT FK_Transactions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );";
                ExecuteCommand(connection, transactionsTable);

                // --- 4. Goals Tablosu ---
                var goalsTable = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Goals' AND xtype='U')
                    CREATE TABLE Goals (
                        GoalId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        Title NVARCHAR(100) NOT NULL,
                        TargetAmount DECIMAL(18, 2) NOT NULL,
                        CurrentAmount DECIMAL(18, 2) DEFAULT 0,
                        Deadline DATETIME2 NULL,
                        Description NVARCHAR(250) NULL,
                        CreatedAt DATETIME2 DEFAULT GETDATE(),
                        CONSTRAINT FK_Goals_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );";
                ExecuteCommand(connection, goalsTable);

                // --- 5. Investments Tablosu ---
                var investmentsTable = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Investments' AND xtype='U')
                    CREATE TABLE Investments (
                        InvestmentId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        Name NVARCHAR(100) NOT NULL,
                        Symbol NVARCHAR(20) NULL,
                        Quantity DECIMAL(18, 4) NOT NULL,
                        BuyingPrice DECIMAL(18, 4) NOT NULL,
                        PurchaseDate DATETIME2 DEFAULT GETDATE(),
                        CONSTRAINT FK_Investments_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );";
                ExecuteCommand(connection, investmentsTable);
            }
        }

        private void ExecuteCommand(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}