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
                        ImageUrl TEXT
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
                        FOREIGN KEY(UserId) REFERENCES Users(Id),
                        FOREIGN KEY(ComputerId) REFERENCES Computers(Id),
                        FOREIGN KEY(ProductId) REFERENCES Products(Id)
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
            }
        }

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection(ConnectionString);
        }
    }
}
