using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MauiApp1.Models;
using Coursework.Services.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Coursework.Services
{
    public sealed class JournalService : DatabaseService, IJournalService
    {
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;

                try
                {
                    await _db.CreateTableAsync<Journal>();
                    await _db.CreateTableAsync<JournalMood>();
                    await _db.CreateTableAsync<JournalTag>();
                }
                catch (Exception ex)
                {
                    // Handle schema mismatch issues during development.
                    if (ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        await _db.DropTableAsync<JournalMood>();
                        await _db.DropTableAsync<JournalTag>();
                        await _db.DropTableAsync<Journal>();

                        await _db.CreateTableAsync<Journal>();
                        await _db.CreateTableAsync<JournalMood>();
                        await _db.CreateTableAsync<JournalTag>();
                    }
                    else
                    {
                        throw;
                    }
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<JournalEntryDetails?> GetEntryByDateAsync(int userId, DateTime entryDate)
        {
            await InitializeAsync();

            var day = entryDate.Date;
            var start = day;
            var end = day.AddDays(1);

            var matches = await _db.Table<Journal>()
                .Where(j => j.UserId == userId && j.EntryDate >= start && j.EntryDate < end)
                .ToListAsync();

            if (matches.Count == 0) return null;

            // If duplicates exist somehow, keep the most recently updated.
            var journal = matches
                .OrderByDescending(j => j.UpdatedAt)
                .First();

            var secondaryMoodIds = await GetSecondaryMoodIdsAsync(journal.JournalId);
            var tagIds = await GetTagIdsAsync(journal.JournalId);

            return new JournalEntryDetails(
                journal.JournalId,
                journal.UserId,
                journal.EntryDate.Date,
                journal.Title ?? "",
                journal.Content ?? "",
                journal.PrimaryMoodId,
                secondaryMoodIds,
                tagIds,
                journal.CreatedAt,
                journal.UpdatedAt);
        }

        public async Task<JournalEntryDetails?> GetEntryByIdAsync(int journalId)
        {
            await InitializeAsync();

            var journal = await _db.Table<Journal>()
                .Where(j => j.JournalId == journalId)
                .FirstOrDefaultAsync();

            if (journal == null) return null;

            var secondaryMoodIds = await GetSecondaryMoodIdsAsync(journal.JournalId);
            var tagIds = await GetTagIdsAsync(journal.JournalId);

            return new JournalEntryDetails(
                journal.JournalId,
                journal.UserId,
                journal.EntryDate.Date,
                journal.Title ?? "",
                journal.Content ?? "",
                journal.PrimaryMoodId,
                secondaryMoodIds,
                tagIds,
                journal.CreatedAt,
                journal.UpdatedAt);
        }

        public async Task<JournalEntryDetails> UpsertEntryAsync(JournalEntryUpsertRequest request)
        {
            if (request.UserId <= 0) throw new ArgumentException("Invalid user.", nameof(request.UserId));
            if (request.PrimaryMoodId <= 0) throw new ArgumentException("Primary mood is required.", nameof(request.PrimaryMoodId));

            var title = (request.Title ?? "").Trim();
            if (title.Length == 0) throw new ArgumentException("Title is required.", nameof(request.Title));

            var contentHtml = request.ContentHtml ?? "";
            var entryDate = request.EntryDate.Date;

            await InitializeAsync();

            // Enforce "only one per day" by de-duplicating any existing rows for this date.
            var start = entryDate;
            var end = entryDate.AddDays(1);
            var matches = await _db.Table<Journal>()
                .Where(j => j.UserId == request.UserId && j.EntryDate >= start && j.EntryDate < end)
                .ToListAsync();

            Journal? keep = null;
            if (matches.Count > 0)
            {
                keep = matches
                    .OrderByDescending(j => j.UpdatedAt)
                    .First();

                foreach (var dup in matches.Where(j => j.JournalId != keep.JournalId))
                {
                    await _db.ExecuteAsync("DELETE FROM JournalMood WHERE JournalId = ?", dup.JournalId);
                    await _db.ExecuteAsync("DELETE FROM JournalTag WHERE JournalId = ?", dup.JournalId);
                    await _db.DeleteAsync(dup);
                }
            }

            Journal journal;
            var now = DateTime.UtcNow;

            if (keep == null)
            {
                journal = new Journal
                {
                    UserId = request.UserId,
                    EntryDate = entryDate,
                    Title = title,
                    Content = contentHtml,
                    PrimaryMoodId = request.PrimaryMoodId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await _db.InsertAsync(journal);
            }
            else
            {
                // Only allow edits within 24 hours of creation.
                if (now - keep.CreatedAt > TimeSpan.FromHours(24))
                {
                    throw new InvalidOperationException("Edits are only allowed within 24 hours of creation.");
                }

                journal = new Journal
                {
                    JournalId = keep.JournalId,
                    UserId = keep.UserId,
                    EntryDate = entryDate,
                    Title = title,
                    Content = contentHtml,
                    PrimaryMoodId = request.PrimaryMoodId,
                    CreatedAt = keep.CreatedAt,
                    UpdatedAt = now
                };

                await _db.UpdateAsync(journal);
            }

            var journalId = journal.JournalId;

            // Normalize selections.
            var secondaryMoodIds = (request.SecondaryMoodIds ?? Array.Empty<int>())
                .Where(id => id > 0 && id != request.PrimaryMoodId)
                .Distinct()
                .Take(2)
                .ToList();

            var tagIds = (request.TagIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .Take(20)
                .ToList();

            await _db.ExecuteAsync("DELETE FROM JournalMood WHERE JournalId = ?", journalId);
            await _db.ExecuteAsync("DELETE FROM JournalTag WHERE JournalId = ?", journalId);

            foreach (var moodId in secondaryMoodIds)
            {
                await _db.InsertAsync(new JournalMood { JournalId = journalId, MoodId = moodId });
            }

            foreach (var tagId in tagIds)
            {
                await _db.InsertAsync(new JournalTag { JournalId = journalId, TagId = tagId });
            }

            return new JournalEntryDetails(
                journalId,
                journal.UserId,
                entryDate,
                journal.Title ?? "",
                journal.Content ?? "",
                journal.PrimaryMoodId,
                secondaryMoodIds,
                tagIds,
                journal.CreatedAt,
                journal.UpdatedAt);
        }

        public async Task<bool> DeleteEntryAsync(int journalId)
        {
            await InitializeAsync();

            var existing = await _db.Table<Journal>()
                .Where(j => j.JournalId == journalId)
                .FirstOrDefaultAsync();

            if (existing == null) return false;

            // Only allow deletes within 24 hours of creation.
            if (DateTime.UtcNow - existing.CreatedAt > TimeSpan.FromHours(24))
            {
                throw new InvalidOperationException("Deletes are only allowed within 24 hours of creation.");
            }

            await _db.ExecuteAsync("DELETE FROM JournalMood WHERE JournalId = ?", journalId);
            await _db.ExecuteAsync("DELETE FROM JournalTag WHERE JournalId = ?", journalId);
            await _db.DeleteAsync(existing);
            return true;
        }

        public async Task<PagedResult<JournalListItem>> GetEntriesPageAsync(int userId, int page, int pageSize, int? moodId = null, int? tagId = null, string? search = null)
        {
            await InitializeAsync();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var baseQuery = _db.Table<Journal>().Where(j => j.UserId == userId);

            if (moodId.HasValue && moodId.Value > 0)
            {
                var moodJournalIds = await GetJournalIdsByMoodAsync(userId, moodId.Value);
                if (moodJournalIds.Count == 0)
                {
                    return new PagedResult<JournalListItem>(Array.Empty<JournalListItem>(), page, pageSize, 0);
                }
                baseQuery = baseQuery.Where(j => moodJournalIds.Contains(j.JournalId));
            }

            if (tagId.HasValue && tagId.Value > 0)
            {
                var tagJournalIds = await GetJournalIdsByTagAsync(userId, tagId.Value);
                if (tagJournalIds.Count == 0)
                {
                    return new PagedResult<JournalListItem>(Array.Empty<JournalListItem>(), page, pageSize, 0);
                }
                baseQuery = baseQuery.Where(j => tagJournalIds.Contains(j.JournalId));
            }

            var searchTrimmed = (search ?? "").Trim();
            if (searchTrimmed.Length > 0)
            {
                baseQuery = baseQuery.Where(j =>
                    (j.Title != null && j.Title.Contains(searchTrimmed)) ||
                    (j.Content != null && j.Content.Contains(searchTrimmed)));
            }

            var query = baseQuery
                .OrderByDescending(j => j.CreatedAt)
                .ThenByDescending(j => j.EntryDate);

            var total = await query.CountAsync();
            var skip = (page - 1) * pageSize;

            var journals = await query.Skip(skip).Take(pageSize).ToListAsync();
            var items = new List<JournalListItem>(journals.Count);

            foreach (var j in journals)
            {
                var secondaryMoodIds = await GetSecondaryMoodIdsAsync(j.JournalId);
                var tagIds = await GetTagIdsAsync(j.JournalId);
                items.Add(new JournalListItem(
                    j.JournalId,
                    j.EntryDate.Date,
                    j.Title ?? "",
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.PrimaryMoodId,
                    secondaryMoodIds,
                    tagIds,
                    BuildPreview(j.Content ?? "")));
            }

            return new PagedResult<JournalListItem>(items, page, pageSize, total);
        }

        private async Task<List<int>> GetJournalIdsByMoodAsync(int userId, int moodId)
        {
            var fromPrimaryRows = await _db.Table<Journal>()
                .Where(j => j.UserId == userId && j.PrimaryMoodId == moodId)
                .ToListAsync();
            var fromPrimary = fromPrimaryRows.Select(j => j.JournalId).ToList();

            var userRows = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();
            var userSet = new HashSet<int>(userRows.Select(j => j.JournalId));

            var fromSecondary = await _db.Table<JournalMood>()
                .Where(jm => jm.MoodId == moodId)
                .ToListAsync();
            var secondaryIds = fromSecondary
                .Where(jm => userSet.Contains(jm.JournalId))
                .Select(jm => jm.JournalId)
                .Distinct()
                .ToList();
            return fromPrimary.Union(secondaryIds).Distinct().ToList();
        }

        private async Task<List<int>> GetJournalIdsByTagAsync(int userId, int tagId)
        {
            var userRows = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();
            var userSet = new HashSet<int>(userRows.Select(j => j.JournalId));

            var tagged = await _db.Table<JournalTag>()
                .Where(jt => jt.TagId == tagId)
                .ToListAsync();
            return tagged
                .Where(jt => userSet.Contains(jt.JournalId))
                .Select(jt => jt.JournalId)
                .Distinct()
                .ToList();
        }

        public async Task<IReadOnlyList<JournalListItem>> GetEntriesInRangeAsync(int userId, DateTime start, DateTime end)
        {
            await InitializeAsync();

            var normalizedStart = start.Date;
            var normalizedEnd = end.Date;

            if (normalizedEnd <= normalizedStart)
            {
                normalizedEnd = normalizedStart.AddDays(1);
            }

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId && j.EntryDate >= normalizedStart && j.EntryDate < normalizedEnd)
                .OrderBy(j => j.EntryDate)
                .ThenBy(j => j.CreatedAt)
                .ToListAsync();

            var items = new List<JournalListItem>(journals.Count);

            foreach (var j in journals)
            {
                var secondaryMoodIds = await GetSecondaryMoodIdsAsync(j.JournalId);
                var tagIds = await GetTagIdsAsync(j.JournalId);

                items.Add(new JournalListItem(
                    j.JournalId,
                    j.EntryDate.Date,
                    j.Title ?? string.Empty,
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.PrimaryMoodId,
                    secondaryMoodIds,
                    tagIds,
                    BuildPreview(j.Content ?? string.Empty)));
            }

            return items;
        }

        public async Task<StreakStats> GetStreakStatsAsync(int userId, DateTime? today = null)
        {
            await InitializeAsync();

            var day = (today ?? DateTime.Today).Date;

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();

            var dates = journals
                .Select(j => j.EntryDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var totalEntries = dates.Count;
            if (dates.Count == 0)
            {
                return new StreakStats(0, 0, 0, 0);
            }

            var dateSet = new HashSet<DateTime>(dates);
            var lastEntryDate = dates[^1];

            // Current streak calculation:
            // Rule 1: Streak must end on today's date - if latest entry is not today, streak = 0
            // Rule 2: Must have at least 2 consecutive days (streak = consecutive days - 1)
            // Rule 3: Count backward day-by-day from today until a gap is found
            var current = 0;
            if (lastEntryDate == day && dateSet.Contains(day))
            {
                var runLen = 0;
                var cursor = day;
                // Count backward day-by-day, stopping at first gap
                while (cursor >= dates[0] && dateSet.Contains(cursor))
                {
                    runLen++;
                    cursor = cursor.AddDays(-1);
                }

                // Streak = consecutive days - 1 (minimum 2 days needed for streak = 1)
                current = Math.Max(0, runLen - 1);
            }

            // Longest streak across history.
            // Uses the same "consecutive links" unit as CurrentStreakDays.
            var longestRunLen = 1;
            var run = 1;
            for (var i = 1; i < dates.Count; i++)
            {
                if (dates[i] == dates[i - 1].AddDays(1))
                {
                    run++;
                    if (run > longestRunLen) longestRunLen = run;
                }
                else
                {
                    run = 1;
                }
            }
            var longest = Math.Max(0, longestRunLen - 1);

            // Missed days: count days since the most recent entry up to today.
            // - If you wrote today (maintaining streak), missed = 0
            // - If latest entry is before today, missed = days between last entry and today
            var missed = 0;
            if (lastEntryDate < day)
            {
                missed = (day - lastEntryDate).Days;
            }

            return new StreakStats(current, longest, missed, totalEntries);
        }

        public async Task<int?> GetMostFrequentPrimaryMoodIdAsync(int userId)
        {
            await InitializeAsync();

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();

            if (journals.Count == 0) return null;

            return journals
                .GroupBy(j => j.PrimaryMoodId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First()
                .Key;
        }

        public async Task<IReadOnlyList<WordCountPoint>> GetRecentWordCountsAsync(int userId, int maxEntries)
        {
            await InitializeAsync();

            if (maxEntries <= 0) return Array.Empty<WordCountPoint>();

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.EntryDate)
                .ThenByDescending(j => j.CreatedAt)
                .Take(maxEntries)
                .ToListAsync();

            if (journals.Count == 0) return Array.Empty<WordCountPoint>();

            var points = journals
                .Select(j => new WordCountPoint(
                    j.EntryDate.Date,
                    CountWordsFromHtml(j.Content ?? string.Empty)))
                .ToList();

            return points;
        }

        public async Task<IReadOnlyList<MoodBreakdownItem>> GetMoodBreakdownAsync(int userId, int maxMoods)
        {
            await InitializeAsync();

            if (maxMoods <= 0) return Array.Empty<MoodBreakdownItem>();

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();

            if (journals.Count == 0) return Array.Empty<MoodBreakdownItem>();

            var journalIds = new HashSet<int>(journals.Select(j => j.JournalId));

            // Start with primary mood counts.
            var moodCounts = new Dictionary<int, int>();
            foreach (var j in journals)
            {
                if (j.PrimaryMoodId <= 0) continue;
                if (!moodCounts.ContainsKey(j.PrimaryMoodId))
                {
                    moodCounts[j.PrimaryMoodId] = 0;
                }
                moodCounts[j.PrimaryMoodId]++;
            }

            // Include secondary moods as well.
            var secondaryRows = await _db.Table<JournalMood>().ToListAsync();
            foreach (var row in secondaryRows)
            {
                if (!journalIds.Contains(row.JournalId)) continue;
                if (row.MoodId <= 0) continue;

                if (!moodCounts.ContainsKey(row.MoodId))
                {
                    moodCounts[row.MoodId] = 0;
                }
                moodCounts[row.MoodId]++;
            }

            if (moodCounts.Count == 0) return Array.Empty<MoodBreakdownItem>();

            var items = moodCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Take(maxMoods)
                .Select(kvp => new MoodBreakdownItem(kvp.Key, kvp.Value))
                .ToList();

            return items;
        }

        public async Task<IReadOnlyList<TagUsageItem>> GetTopTagsAsync(int userId, int maxTags)
        {
            await InitializeAsync();

            if (maxTags <= 0) return Array.Empty<TagUsageItem>();

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .ToListAsync();

            if (journals.Count == 0) return Array.Empty<TagUsageItem>();

            var journalIds = new HashSet<int>(journals.Select(j => j.JournalId));

            var allTags = await _db.Table<JournalTag>().ToListAsync();
            var userTags = allTags.Where(t => journalIds.Contains(t.JournalId));

            var items = userTags
                .Where(t => t.TagId > 0)
                .GroupBy(t => t.TagId)
                .Select(g => new TagUsageItem(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.TagId)
                .Take(maxTags)
                .ToList();

            return items;
        }

        public async Task<byte[]> ExportAllEntriesPdfAsync(int userId)
        {
            await InitializeAsync();

            var journals = await _db.Table<Journal>()
                .Where(j => j.UserId == userId)
                .OrderBy(j => j.EntryDate)
                .ThenBy(j => j.CreatedAt)
                .ToListAsync();

            if (journals.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var items = new List<JournalListItem>(journals.Count);

            foreach (var j in journals)
            {
                var secondaryMoodIds = await GetSecondaryMoodIdsAsync(j.JournalId);
                var tagIds = await GetTagIdsAsync(j.JournalId);

                items.Add(new JournalListItem(
                    j.JournalId,
                    j.EntryDate.Date,
                    j.Title ?? string.Empty,
                    j.CreatedAt,
                    j.UpdatedAt,
                    j.PrimaryMoodId,
                    secondaryMoodIds,
                    tagIds,
                    BuildPreview(j.Content ?? string.Empty)));
            }

            // Configure QuestPDF license for this export (Community license)
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Journal Entries")
                        .SemiBold().FontSize(24).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(col =>
                        {
                            col.Spacing(14);

                            foreach (var item in items)
                            {
                                col.Item().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                                    .PaddingBottom(6)
                                    .Column(entry =>
                                    {
                                        entry.Item().Text($"{item.EntryDate:dddd, dd MMM yyyy}")
                                            .SemiBold().FontSize(14);

                                        if (!string.IsNullOrWhiteSpace(item.Title))
                                        {
                                            entry.Item().Text(item.Title)
                                                .FontSize(12)
                                                .FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                                        }

                                        if (!string.IsNullOrWhiteSpace(item.ContentPreview))
                                            {
                                                entry.Item().Text(item.ContentPreview)
                                                    .FontSize(11)
                                                    .FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                                        }
                                    });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            })
            .GeneratePdf();

            return pdfBytes;
        }

        public async Task<byte[]> ExportEntryPdfAsync(int journalId)
        {
            await InitializeAsync();

            var entry = await GetEntryByIdAsync(journalId);
            if (entry == null)
            {
                return Array.Empty<byte>();
            }

            // Configure QuestPDF license for this export (Community license)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var plainText = Regex.Replace(entry.ContentHtml ?? string.Empty, "<.*?>", string.Empty).Trim();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text(text =>
                        {
                            text.Line(entry.Title ?? "Journal Entry").SemiBold().FontSize(20).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                            text.Line(entry.EntryDate.ToString("dddd, dd MMM yyyy"));
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(col =>
                        {
                            col.Spacing(10);

                            if (!string.IsNullOrWhiteSpace(plainText))
                            {
                                col.Item().Text(plainText).FontSize(12);
                            }
                            else
                            {
                                col.Item().Text("No content.").Italic().FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            })
            .GeneratePdf();

            return pdfBytes;
        }

        private async Task<IReadOnlyList<int>> GetSecondaryMoodIdsAsync(int journalId)
        {
            var rows = await _db.Table<JournalMood>()
                .Where(jm => jm.JournalId == journalId)
                .ToListAsync();

            return rows
                .Select(r => r.MoodId)
                .Where(id => id > 0)
                .Distinct()
                .Take(2)
                .ToList();
        }

        private async Task<IReadOnlyList<int>> GetTagIdsAsync(int journalId)
        {
            var rows = await _db.Table<JournalTag>()
                .Where(jt => jt.JournalId == journalId)
                .ToListAsync();

            return rows
                .Select(r => r.TagId)
                .Where(id => id > 0)
                .Distinct()
                .Take(20)
                .ToList();
        }

        private static string BuildPreview(string html)
        {
            var text = Regex.Replace(html ?? "", "<.*?>", string.Empty);
            text = (text ?? "").Trim();
            if (text.Length <= 160) return text;
            return text.Substring(0, 160).TrimEnd() + "…";
        }

        private static int CountWordsFromHtml(string html)
        {
            var text = Regex.Replace(html ?? "", "<.*?>", string.Empty);
            text = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) return 0;

            return Regex.Matches(text, @"\b\w+\b").Count;
        }
    }
}

