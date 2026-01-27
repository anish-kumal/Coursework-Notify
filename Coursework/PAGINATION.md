# Pagination Documentation

## Overview
The journal entries page uses server-side pagination to display entries in manageable chunks. The default page size is **5 entries per page**.

## Components

### 1. PagedResult Model
Located in `Services/JournalModels.cs`, this record wraps paginated data:

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,      // The items for current page
    int Page,                     // Current page number (1-based)
    int PageSize,                 // Items per page
    int TotalCount)               // Total items across all pages
{
    public int TotalPages => ...; // Calculated total pages
    public bool HasPrevious => ...; // Can go to previous page?
    public bool HasNext => ...;     // Can go to next page?
}
```

### 2. Service Method: GetEntriesPageAsync
Located in `Services/JournalService.cs` (lines 262-326).

**How it works:**
1. Validates page number (minimum 1) and page size (minimum 10)
2. Builds base query filtered by userId
3. Applies optional filters (mood, tag, search)
4. Orders results by `CreatedAt` (descending), then `EntryDate` (descending)
5. Calculates total count
6. Uses `Skip()` and `Take()` to get only the current page's items:
   ```csharp
   var skip = (page - 1) * pageSize;
   var journals = await query.Skip(skip).Take(pageSize).ToListAsync();
   ```
7. Returns `PagedResult<JournalListItem>` with items, page info, and total count

### 3. UI Component: JournalEntries.razor
Located in `Components/Pages/JournalEntries.razor`.

**Key features:**
- Reads page number from URL query parameter: `?page=1`
- Page size is hardcoded to **5** (line 152)
- Calls `JournalService.GetEntriesPageAsync()` in `LoadAsync()` method
- Displays pagination controls at bottom:
  - Previous button (disabled if `HasPrevious` is false)
  - Page info: "Page X of Y"
  - Next button (disabled if `HasNext` is false)

**Navigation:**
- `GoToPage(int p)` method updates URL query parameters while preserving filters
- URL format: `/journal?page=2&mood=1&tag=3&q=search`
- Uses `Navigation.NavigateTo()` with `forceLoad: false` for client-side navigation

## Flow Example

1. User visits `/journal?page=2`
2. Component reads `page=2` from query string
3. `LoadAsync()` calls `GetEntriesPageAsync(userId, 2, 5, ...)`
4. Service calculates: `skip = (2-1) * 5 = 5`, takes next 5 items
5. Returns `PagedResult` with items 6-10 (if they exist)
6. UI displays those 5 items and pagination controls

## Filter Persistence
When navigating pages, filters are preserved in the URL:
- Mood filter: `?mood=1`
- Tag filter: `?tag=3`
- Search query: `?q=keyword`

The `GoToPage()` method rebuilds the query string with all active filters.
