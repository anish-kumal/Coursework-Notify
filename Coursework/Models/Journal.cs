using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MauiApp1.Models
{

    public class Journal
    {
        [PrimaryKey, AutoIncrement]
        public int JournalId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime EntryDate { get; set; }

        [Required]
        [StringLength(150)]
        public string? Title { get; set; }

        [Required]
        public string? Content { get; set; }

        // Primary Mood
        [Required]
        public int PrimaryMoodId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
