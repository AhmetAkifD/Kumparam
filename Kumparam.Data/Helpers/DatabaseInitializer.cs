using System;
using Microsoft.Data.SqlClient;

namespace Kumparam.Data.Helpers
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
            CreateDatabaseIfNotExists();
            CreateTablesIfNotExist();
            InsertDefaultCategories(); // Varsayılan kategorileri ekle
        }

        private void CreateDatabaseIfNotExists()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            string targetDatabaseName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
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
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // --- 1. Users Tablosu ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                    CREATE TABLE Users (
                        UserId UNIQUEIDENTIFIER PRIMARY KEY,
                        Email NVARCHAR(256) NOT NULL UNIQUE,
                        PasswordHash VARBINARY(64) NOT NULL,
                        PasswordSalt VARBINARY(16) NOT NULL,
                        IsAdmin BIT DEFAULT 0,
                        CreatedAt DATETIME2 DEFAULT GETDATE()
                    );");

                // --- 2. UserProfiles Tablosu ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserProfiles' AND xtype='U')
                    CREATE TABLE UserProfiles (
                        UserId UNIQUEIDENTIFIER PRIMARY KEY,
                        FirstName NVARCHAR(100),
                        LastName NVARCHAR(100),
                        CONSTRAINT FK_UserProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );");

                // --- 3. Transactions Tablosu ---
                ExecuteCommand(connection, @"
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
                    );");

                // --- 4. DeletedTransactions (Çöp Kutusu) ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DeletedTransactions' AND xtype='U')
                    CREATE TABLE DeletedTransactions (
                        DeletedId INT PRIMARY KEY IDENTITY(1,1),
                        OriginalTransactionId UNIQUEIDENTIFIER,
                        UserId UNIQUEIDENTIFIER,
                        Amount DECIMAL(18, 2),
                        Type NVARCHAR(20),
                        Category NVARCHAR(50),
                        Description NVARCHAR(200),
                        TransactionDate DATETIME2,
                        DeletedAt DATETIME2 DEFAULT GETDATE(),
                        IsHidden BIT DEFAULT 0
                    );");

                // --- 5. Goals Tablosu ---
                ExecuteCommand(connection, @"
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
                    );");

                // --- 6. Investments Tablosu ---
                ExecuteCommand(connection, @"
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
                    );");

                // --- 7. ScrapingConfigs Tablosu ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ScrapingConfigs' AND xtype='U')
                    CREATE TABLE ScrapingConfigs (
                        ConfigId INT PRIMARY KEY IDENTITY(1,1),
                        Symbol NVARCHAR(20) NOT NULL UNIQUE,
                        TargetUrl NVARCHAR(500) NOT NULL,
                        HtmlPath_Buying NVARCHAR(500),
                        HtmlPath_Selling NVARCHAR(500),
                        SourceType NVARCHAR(50) DEFAULT 'Web',
                        IsActive BIT DEFAULT 1,
                        Description NVARCHAR(200)
                    );");

                // --- 8. Categories Tablosu ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                    CREATE TABLE Categories (
                        CategoryId INT PRIMARY KEY IDENTITY(1,1),
                        CategoryName NVARCHAR(50) NOT NULL UNIQUE,
                        IsActive BIT DEFAULT 1
                    );");

                // --- 9. YENİ: AutoTransactions (Otomatik Gelir/Gider) ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AutoTransactions' AND xtype='U')
                    CREATE TABLE AutoTransactions (
                        AutoId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        Amount DECIMAL(18, 2) NOT NULL,
                        Type NVARCHAR(20) NOT NULL,
                        Category NVARCHAR(50),
                        Description NVARCHAR(200),
                        Frequency NVARCHAR(20) NOT NULL, -- 'Daily', 'Weekly', 'Monthly'
                        NextRunDate DATETIME2 NOT NULL,
                        IsActive BIT DEFAULT 1,
                        CONSTRAINT FK_AutoTransactions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                    );");

                // --- 10. YENİ: GoalAutomations (Hedeflere Otomatik Para Aktarma) ---
                ExecuteCommand(connection, @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GoalAutomations' AND xtype='U')
                    CREATE TABLE GoalAutomations (
                        AutomationId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                        UserId UNIQUEIDENTIFIER NOT NULL,
                        GoalId UNIQUEIDENTIFIER NOT NULL,
                        Amount DECIMAL(18, 2) NOT NULL,
                        Frequency NVARCHAR(20) NOT NULL, -- 'Daily', 'Weekly', 'Monthly'
                        NextRunDate DATETIME2 NOT NULL,
                        IsActive BIT DEFAULT 1,
                        CONSTRAINT FK_GoalAutomations_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
                        CONSTRAINT FK_GoalAutomations_Goals FOREIGN KEY (GoalId) REFERENCES Goals(GoalId) ON DELETE NO ACTION
                    );");
            }
        }

        private void InsertDefaultCategories()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM Categories)
                    BEGIN
                        INSERT INTO Categories (CategoryName) VALUES 
                        (N'Maaş'), (N'Gıda'), (N'Kira'), (N'Ulaşım'), (N'Eğlence'), (N'Yatırım'), (N'Diğer');
                    END";
                ExecuteCommand(connection, sql);
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