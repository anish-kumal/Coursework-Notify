using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace Coursework.Services
{
    public class TagService : DatabaseService, ITagService
    {
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private static readonly string[] PredefinedTagNames = new[]
        {
            "Work","Career","Studies","Family","Friends","Relationships",
            "Health","Fitness","Personal Growth","Self-care","Hobbies",
            "Travel","Nature","Finance","Spirituality",
            "Birthday","Holiday","Vacation","Celebration",
            "Exercise","Reading","Writing","Cooking",
            "Meditation","Yoga","Music","Shopping",
            "Parenting","Projects","Planning","Reflection"
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
                    await _db.CreateTableAsync<Tag>();
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        await _db.DropTableAsync<Tag>();
                        await _db.CreateTableAsync<Tag>();
                    }
                    else
                    {
                        throw;
                    }
                }

                var existing = await _db.Table<Tag>().ToListAsync();
                var existingNames = new HashSet<string>(
                    existing.Select(t => t.TagName ?? ""),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var name in PredefinedTagNames)
                {
                    if (existingNames.Contains(name)) continue;
                    await _db.InsertAsync(new Tag { TagName = name, IsPredefined = true });
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<List<Tag>> GetAllTagsAsync()
        {
            await InitializeAsync();
            return await _db.Table<Tag>()
                .OrderBy(t => t.TagName)
                .ToListAsync();
        }

        public async Task<Tag?> CreateTagAsync(string tagName)
        {
            await InitializeAsync();

            var name = (tagName ?? "").Trim();
            if (name.Length == 0) return null;

            var all = await _db.Table<Tag>().ToListAsync();
            if (all.Any(t => string.Equals(t.TagName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var tag = new Tag { TagName = name, IsPredefined = false };
            await _db.InsertAsync(tag);
            return tag;
        }

        public async Task<bool> UpdateTagAsync(Tag tag)
        {
            await InitializeAsync();

            var name = (tag.TagName ?? "").Trim();
            if (name.Length == 0) return false;

            var existing = await _db.Table<Tag>()
                .Where(t => t.TagId == tag.TagId)
                .FirstOrDefaultAsync();

            if (existing == null || existing.IsPredefined) return false;

            var all = await _db.Table<Tag>().ToListAsync();
            if (all.Any(t => t.TagId != tag.TagId && string.Equals(t.TagName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            existing.TagName = name;
            await _db.UpdateAsync(existing);
            return true;
        }

        public async Task<bool> DeleteTagAsync(int tagId)
        {
            await InitializeAsync();

            var existing = await _db.Table<Tag>()
                .Where(t => t.TagId == tagId)
                .FirstOrDefaultAsync();

            if (existing == null || existing.IsPredefined) return false;

            // Remove from any entries first (so views don't break).
            try
            {
                await _db.ExecuteAsync("DELETE FROM JournalTag WHERE TagId = ?", tagId);
            }
            catch
            {
                // Ignore if table doesn't exist yet.
            }

            await _db.DeleteAsync(existing);
            return true;
        }
    }
}