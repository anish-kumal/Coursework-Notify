using System.Threading.Tasks;

namespace Coursework.Services
{
    public interface IUserService
    {
        Task InitializeAsync();
        Task<bool> UserExistsAsync();
        Task<bool> RegisterUserAsync(string username, string pin);
        Task<bool> ValidateLoginAsync(string pin);
        Task<bool> ChangePinAsync(string username, string newPin);
        Task<bool> UsernameExistsAsync(string username);
        Task<int?> GetSingleUserIdAsync();
    }
}   