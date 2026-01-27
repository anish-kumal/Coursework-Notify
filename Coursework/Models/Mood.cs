using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class Mood
    {
        [PrimaryKey, AutoIncrement]
        public int MoodId { get; set; }

        [Required]
        public string? MoodName { get; set; }

        [Required]
        public int MoodCategoryId { get; set; }

        public bool IsPredefined { get; set; }

    }
}
