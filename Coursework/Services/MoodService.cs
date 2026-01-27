using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace Coursework.Services
{
    public class MoodService : DatabaseService, IMoodService
    {
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private static readonly (string CategoryName, string[] MoodNames)[] Predefined = new[]
        {
            ("Positive", new[] { "Happy", "Excited", "Relaxed", "Grateful", "Confident" }),
            ("Neutral",  new[] { "Calm", "Thoughtful", "Curious", "Nostalgic", "Bored" }),
            ("Negative", new[] { "Sad", "Angry", "Stressed", "Lonely", "Anxious" }),
        };

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;

                try
                {
                    await _db.CreateTableAsync<MoodCategory>();
                    await _db.CreateTableAsync<Mood>();
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        await _db.DropTableAsync<Mood>();
                        await _db.DropTableAsync<MoodCategory>();
                        await _db.CreateTableAsync<MoodCategory>();
                        await _db.CreateTableAsync<Mood>();
                    }
                    else
                    {
                        throw;
                    }
                }

                var categories = await _db.Table<MoodCategory>().ToListAsync();
                var categoryByName = categories.ToDictionary(c => c.Name ?? "", c => c, StringComparer.OrdinalIgnoreCase);

                foreach (var (categoryName, _) in Predefined)
                {
                    if (categoryByName.ContainsKey(categoryName)) continue;
                    var cat = new MoodCategory { Name = categoryName, IsPredefined = true };
                    await _db.InsertAsync(cat);
                    categoryByName[categoryName] = cat;
                }

                // Reload categories to ensure IDs are populated.
                categories = await _db.Table<MoodCategory>().ToListAsync();
                categoryByName = categories.ToDictionary(c => c.Name ?? "", c => c, StringComparer.OrdinalIgnoreCase);

                var moods = await _db.Table<Mood>().ToListAsync();
                var moodNames = new HashSet<string>(moods.Select(m => m.MoodName ?? ""), StringComparer.OrdinalIgnoreCase);

                foreach (var (categoryName, moodNamesForCat) in Predefined)
                {
                    if (!categoryByName.TryGetValue(categoryName, out var cat)) continue;
                    foreach (var moodName in moodNamesForCat)
                    {
                        if (moodNames.Contains(moodName)) continue;
                        await _db.InsertAsync(new Mood
                        {
                            MoodName = moodName,
                            MoodCategoryId = cat.MoodCategoryId,
                            IsPredefined = true
                        });
                        moodNames.Add(moodName);
                    }
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<List<MoodCategory>> GetAllCategoriesAsync()
        {
            await InitializeAsync();
            return await _db.Table<MoodCategory>()
                .OrderBy(c => c.MoodCategoryId)
                .ToListAsync();
        }

        public async Task<List<Mood>> GetAllMoodsAsync()
        {
            await InitializeAsync();
            return await _db.Table<Mood>()
                .OrderBy(m => m.MoodCategoryId)
                .ThenBy(m => m.MoodName)
                .ToListAsync();
        }

        public async Task<Mood?> CreateMoodAsync(string moodName, int moodCategoryId)
        {
            await InitializeAsync();

            moodName = (moodName ?? "").Trim();
            if (moodName.Length == 0) return null;

            var category = await _db.Table<MoodCategory>()
                .Where(c => c.MoodCategoryId == moodCategoryId)
                .FirstOrDefaultAsync();

            if (category == null)
            {
                throw new ArgumentException("Invalid mood category.", nameof(moodCategoryId));
            }

            var all = await _db.Table<Mood>().ToListAsync();
            if (all.Any(m => string.Equals(m.MoodName, moodName, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var mood = new Mood
            {
                MoodName = moodName,
                MoodCategoryId = moodCategoryId,
                IsPredefined = false
            };

            await _db.InsertAsync(mood);
            return mood;
        }

        public async Task<bool> UpdateMoodAsync(Mood mood)
        {
            await InitializeAsync();

            var existing = await _db.Table<Mood>()
                .Where(m => m.MoodId == mood.MoodId)
                .FirstOrDefaultAsync();

            if (existing == null || existing.IsPredefined) return false;

            var newName = (mood.MoodName ?? "").Trim();
            if (newName.Length == 0) return false;

            var all = await _db.Table<Mood>().ToListAsync();
            if (all.Any(m => m.MoodId != mood.MoodId && string.Equals(m.MoodName, newName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var category = await _db.Table<MoodCategory>()
                .Where(c => c.MoodCategoryId == mood.MoodCategoryId)
                .FirstOrDefaultAsync();

            if (category == null) return false;

            existing.MoodName = newName;
            existing.MoodCategoryId = mood.MoodCategoryId;
            await _db.UpdateAsync(existing);
            return true;
        }

        public async Task<bool> DeleteMoodAsync(int moodId)
        {
            await InitializeAsync();

            var existing = await _db.Table<Mood>()
                .Where(m => m.MoodId == moodId)
                .FirstOrDefaultAsync();

            if (existing == null || existing.IsPredefined) return false;

            // Prevent deleting moods that are in use.
            try
            {
                var usedAsPrimary = await _db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Journal WHERE PrimaryMoodId = ?",
                    moodId);

                var usedAsSecondary = await _db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM JournalMood WHERE MoodId = ?",
                    moodId);

                if (usedAsPrimary > 0 || usedAsSecondary > 0) return false;
            }
            catch
            {
                // If journal tables don't exist yet, just proceed.
            }

            await _db.DeleteAsync(existing);
            return true;
        }
    }
}

