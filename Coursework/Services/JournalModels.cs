using System;
using System.Collections.Generic;

namespace Coursework.Services.Models
{
    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount)
    {
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }

    public sealed record JournalListItem(
        int JournalId,
        DateTime EntryDate,
        string Title,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int PrimaryMoodId,
        IReadOnlyList<int> SecondaryMoodIds,
        IReadOnlyList<int> TagIds,
        string ContentPreview);

    public sealed record JournalEntryDetails(
        int JournalId,
        int UserId,
        DateTime EntryDate,
        string Title,
        string ContentHtml,
        int PrimaryMoodId,
        IReadOnlyList<int> SecondaryMoodIds,
        IReadOnlyList<int> TagIds,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public sealed record JournalEntryUpsertRequest(
        int UserId,
        DateTime EntryDate,
        string Title,
        string ContentHtml,
        int PrimaryMoodId,
        IReadOnlyList<int> SecondaryMoodIds,
        IReadOnlyList<int> TagIds);

    public sealed record StreakStats(
        int CurrentStreakDays,
        int LongestStreakDays,
        int MissedDays,
        int TotalEntries);

    public sealed record WordCountPoint(
        DateTime EntryDate,
        int WordCount);

    public sealed record MoodBreakdownItem(
        int MoodId,
        int Count);

    public sealed record TagUsageItem(
        int TagId,
        int Count);
}

