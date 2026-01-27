using System;
using SQLite;

namespace Coursework.Services
{
    public abstract class DatabaseService
    {
        protected readonly SQLiteAsyncConnection _db;

        protected DatabaseService()
        {
            var dbPath = Path.Combine(
                FileSystem.AppDataDirectory,
                "journal_mgmt.db3");
            Console.WriteLine("hello hello " + dbPath);

            _db = new SQLiteAsyncConnection(dbPath);
        }
    }
}