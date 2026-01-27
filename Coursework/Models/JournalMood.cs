using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class JournalMood
    {
        [PrimaryKey, AutoIncrement]
        public int JournalMoodId { get; set; }

        [Required]
        public int JournalId { get; set; }

        [Required]
        public int MoodId { get; set; }
    }
}
