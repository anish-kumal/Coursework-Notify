using System;
using System.Linq;
using System.Threading.Tasks;
using Coursework.Services.Models;
using MauiApp1.Models;

namespace Coursework.Services
{
    /// <summary>
    /// Helper to seed realistic journal data on past dates
    /// for testing streaks, longest streak, etc.
    /// </summary>
    public static class JournalSeeder
    {
        /// <summary>
        /// Seeds 9 learning journal entries o
        /// days that end yesterday (no entry for today).
        /// Call this once during development / testing.
        /// </summary>
        /// <param name="journalService">Journal service instance.</param>
        /// <param name="moodService">Mood service instance.</param>
        /// <param name="userId">User id to attach entries to.</param>
        public static async Task SeedSampleJournalAsync(
            IJournalService journalService,
            IMoodService moodService,
            int userId = 1)
        {
            if (journalService is null) throw new ArgumentNullException(nameof(journalService));
            if (moodService is null) throw new ArgumentNullException(nameof(moodService));

            // Ensure tables are created.
            await moodService.InitializeAsync();
            await journalService.InitializeAsync();

            // Pick any existing mood as PrimaryMoodId.
            var moods = await moodService.GetAllMoodsAsync();
            var primaryMood = moods.FirstOrDefault();
            if (primaryMood == null)
            {
                // If somehow no moods exist, just abort.
                return;
            }

            var primaryMoodId = primaryMood.MoodId;

            // We want real dates, but not "today" – end on yesterday.
            var today = DateTime.Today;
            var lastDay = today.AddDays(-1);   // yesterday

            var entries = new[]
            {
                new
                {
                    Date = lastDay.AddDays(-10),
                    Title = "1. Introduction to .NET",
                    Content = "Today I studied the .NET framework in more detail. It is a powerful platform used to develop desktop, web, and mobile applications. I learned about .NET Core, the runtime environment, and how applications are executed."
                },
                new
                {
                    Date = lastDay.AddDays(-9),
                    Title = "2. Understanding Project Structure",
                    Content = "I explored the structure of a .NET project. I learned the purpose of folders such as Models, Services, Controllers, and Views. Understanding the structure helps in organizing code properly."
                },
                new
                {
                    Date = lastDay.AddDays(-8),
                    Title = "3. C# Programming Concepts",
                    Content = "I practiced C# basics including variables, data types, conditions, loops, and functions. I also learned about classes, objects, and constructors."
                },
                new
                {
                    Date = lastDay.AddDays(-7),
                    Title = "4. Object-Oriented Programming",
                    Content = "I studied OOP concepts like encapsulation, inheritance, polymorphism, and abstraction. These concepts help in writing reusable and maintainable code."
                },
                new
                {
                    Date = lastDay.AddDays(-6),
                    Title = "5. Database Connection",
                    Content = "I learned how to connect an SQLite database with .NET. I used connection strings and a data access layer to manage database communication."
                },
                new
                {
                    Date = lastDay.AddDays(-5),
                    Title = "6. CRUD Operations",
                    Content = "I practiced Create, Read, Update, and Delete operations. These operations allow users to manage data effectively in applications."
                },
                new
                {
                    Date = lastDay.AddDays(-4),
                    Title = "7. Validation and Authentication",
                    Content = "I learned how to validate user input such as username, email, and password. Authentication ensures that only authorized users can access the system."
                },
                new
                {
                    Date = lastDay.AddDays(-3),
                    Title = "8. Error Handling and Logging",
                    Content = "I studied exception handling using try–catch blocks. Logging errors helps identify issues during runtime and improves system reliability."
                },
                new
                {
                    Date = lastDay.AddDays(-3),
                    Title = "9. Debugging and Testing",
                    Content = "I used breakpoints and debugging tools to trace program execution. Testing helped me fix logical errors and improve application performance."
                },
            };

            foreach (var e in entries)
            {
                var request = new JournalEntryUpsertRequest(
                    UserId: userId,
                    EntryDate: e.Date,
                    Title: e.Title,
                    ContentHtml: e.Content, // plain text is ok
                    PrimaryMoodId: primaryMoodId,
                    SecondaryMoodIds: Array.Empty<int>(),
                    TagIds: Array.Empty<int>());

                await journalService.UpsertEntryAsync(request);
            }
        }
    }
}

