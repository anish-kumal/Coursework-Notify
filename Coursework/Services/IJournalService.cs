using System;
using System.Threading.Tasks;
using Coursework.Services.Models;

namespace Coursework.Services
{
    public interface IJournalService
    {
        Task InitializeAsync();

        Task<JournalEntryDetails?> GetEntryByDateAsync(int userId, DateTime entryDate);
        Task<JournalEntryDetails?> GetEntryByIdAsync(int journalId);

        /// <summary>
        /// Creates or updates an entry for the given date (only one per day).
        /// </summary>
        Task<JournalEntryDetails> UpsertEntryAsync(JournalEntryUpsertRequest request);

        Task<bool> DeleteEntryAsync(int journalId);

        Task<PagedResult<JournalListItem>> GetEntriesPageAsync(int userId, int page, int pageSize, int? moodId = null, int? tagId = null, string? search = null);

        /// <summary>
        /// Returns all journal entries for the user between the given dates (inclusive of start, exclusive of end).
        /// Intended for calendar-style views.
        /// </summary>
        Task<IReadOnlyList<JournalListItem>> GetEntriesInRangeAsync(int userId, DateTime start, DateTime end);

        Task<StreakStats> GetStreakStatsAsync(int userId, DateTime? today = null);

        Task<int?> GetMostFrequentPrimaryMoodIdAsync(int userId);

        Task<IReadOnlyList<WordCountPoint>> GetRecentWordCountsAsync(int userId, int maxEntries);

        Task<IReadOnlyList<MoodBreakdownItem>> GetMoodBreakdownAsync(int userId, int maxMoods);

        Task<IReadOnlyList<TagUsageItem>> GetTopTagsAsync(int userId, int maxTags);

        /// <summary>
        /// Generates a PDF containing all journal entries for the user.
        /// </summary>
        Task<byte[]> ExportAllEntriesPdfAsync(int userId);

        /// <summary>
        /// Generates a PDF for a single journal entry.
        /// </summary>
        Task<byte[]> ExportEntryPdfAsync(int journalId);
    }
}

