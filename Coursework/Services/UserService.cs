using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SQLite;
using Coursework.Models;

namespace Coursework.Services
{
    public class UserService : DatabaseService, IUserService
    {
        private bool _initialized = false;

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                await _db.CreateTableAsync<User>();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("PRIMARY KEY"))
                {
                    await _db.DropTableAsync<User>();
                    await _db.CreateTableAsync<User>();
                }
                else
                {
                    throw;
                }
            }

            _initialized = true;
        }

        private bool IsValidPin(string pin)
        {
            return Regex.IsMatch(pin, @"^\d{4}$");
        }

        public async Task<bool> UserExistsAsync()
        {
            var count = await _db.Table<User>().CountAsync();
            return count > 0;
        }

        public async Task<bool> RegisterUserAsync(string username, string pin)
        {
            if (!IsValidPin(pin)) return false;

            var count = await _db.Table<User>().CountAsync();
            if (count > 0) return false; // Only one user allowed

            var user = new User
            {
                Username = username,
                PinHash = pin // Consider hashing in production
            };
            await _db.InsertAsync(user);
            return true;
        }

        // Login using PIN only
        public async Task<bool> ValidateLoginAsync(string pin)
        {
            var user = await _db.Table<User>()
                .Where(u => u.PinHash == pin)
                .FirstOrDefaultAsync();
            return user != null;
        }

        public async Task<bool> ChangePinAsync(string username, string newPin)
        {
            if (!IsValidPin(newPin)) return false;

            var user = await _db.Table<User>()
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();
            if (user == null) return false;
            user.PinHash = newPin;
            await _db.UpdateAsync(user);
            return true;
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            await InitializeAsync();
            var user = await _db.Table<User>().FirstOrDefaultAsync();
            return user != null && string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<int?> GetSingleUserIdAsync()
        {
            await InitializeAsync();
            var user = await _db.Table<User>().FirstOrDefaultAsync();
            return user?.UserId;
        }
    }
}