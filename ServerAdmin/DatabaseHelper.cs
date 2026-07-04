using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ServerAdmin
{
    public static class DatabaseHelper
    {
        private static string ConnectionString = "Data Source=QuanLyQuanNet.db";

        public static void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                // Users table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        Password TEXT NOT NULL,
                        Balance DECIMAL NOT NULL DEFAULT 0,
                        Role TEXT NOT NULL DEFAULT 'Client'
                    )");

                // Computers table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Computers (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        Status TEXT NOT NULL DEFAULT 'Available',
                        CurrentUserId INTEGER NULL,
                        FOREIGN KEY(CurrentUserId) REFERENCES Users(Id)
                    )");

                // Products table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Products (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Price DECIMAL NOT NULL,
                        ImageUrl TEXT,
                        SoLuongTon INTEGER NOT NULL DEFAULT 0,
                        Category TEXT
                    )");

                // Orders table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Orders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        ComputerId INTEGER NOT NULL,
                        ProductId INTEGER NOT NULL,
                        Quantity INTEGER NOT NULL DEFAULT 1,
                        Status TEXT NOT NULL DEFAULT 'Pending',
                        Time TEXT NOT NULL,
                        Notes TEXT,
                        FOREIGN KEY(UserId) REFERENCES Users(Id),
                        FOREIGN KEY(ComputerId) REFERENCES Computers(Id),
                        FOREIGN KEY(ProductId) REFERENCES Products(Id)
                    )");

                // Migration: add Notes column for databases created before Notes was in the schema
                try { connection.Execute("ALTER TABLE Orders ADD COLUMN Notes TEXT"); } catch { }

                // ChatMessages table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS ChatMessages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ComputerId INTEGER NOT NULL,
                        UserId INTEGER,
                        Message TEXT NOT NULL,
                        IsFromAdmin INTEGER NOT NULL DEFAULT 0,
                        Timestamp TEXT NOT NULL,
                        IsRead INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY(ComputerId) REFERENCES Computers(Id),
                        FOREIGN KEY(UserId) REFERENCES Users(Id)
                    )");

                // Sessions table
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Sessions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        ComputerId INTEGER NOT NULL,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT,
                        Cost DECIMAL,
                        RemainingSecondsAtCheckpoint REAL DEFAULT 0,
                        LastCheckpointTime TEXT,
                        FOREIGN KEY(UserId) REFERENCES Users(Id),
                        FOREIGN KEY(ComputerId) REFERENCES Computers(Id)
                    )");

                // Check default admin
                var adminCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'");
                if (adminCount == 0)
                {
                    connection.Execute("INSERT INTO Users (Username, Password, Role) VALUES (@Username, @Password, @Role)", 
                        new { Username = "admin", Password = "1", Role = "Admin" });
                }

                // Check default computers
                var compCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Computers");
                if (compCount == 0)
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        connection.Execute("INSERT INTO Computers (Name, Status) VALUES (@Name, 'Available')", 
                            new { Name = "Máy " + i.ToString("D2") });
                    }
                }

                // Check default products (must match client's ServiceWindowForm.BuildSampleProducts)
                var prodCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
                if (prodCount == 0)
                {
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (1, 'Coca Cola', 15000, 10, 'Đồ uống')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (2, 'Pepsi', 15000, 5, 'Đồ uống')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (3, 'Trà Đào', 22000, 0, 'Đồ uống')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (4, 'Sting dâu', 12000, 20, 'Đồ uống')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (5, 'Mì Xào Bò', 35000, 8, 'Đồ ăn')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (6, 'Phở Bò', 40000, 0, 'Đồ ăn')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (7, 'Mì tôm trứng', 25000, 15, 'Đồ ăn')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (8, 'Cơm Sườn', 45000, 6, 'Đồ ăn')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (9, 'Cơm Gà', 42000, 0, 'Đồ ăn')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (10, 'Trà sữa', 20000, 0, 'Đồ uống')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (11, 'Thẻ Garena 50K', 50000, 20, 'Thẻ cào')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (12, 'Thẻ Zing 100K', 100000, 10, 'Thẻ cào')");
                    connection.Execute("INSERT INTO Products (Id, Name, Price, SoLuongTon, Category) VALUES (13, 'Thẻ Steam 200K', 200000, 0, 'Thẻ cào')");
                }
            }
        }

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection(ConnectionString);
        }
    }
}
