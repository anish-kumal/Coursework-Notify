using System.Collections.Generic;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace Coursework.Services
{
    public interface IMoodService
    {
        Task InitializeAsync();
        Task<List<MoodCategory>> GetAllCategoriesAsync();
        Task<List<Mood>> GetAllMoodsAsync();
        Task<Mood?> CreateMoodAsync(string moodName, int moodCategoryId);
        Task<bool> UpdateMoodAsync(Mood mood);
        Task<bool> DeleteMoodAsync(int moodId);
    }
}

