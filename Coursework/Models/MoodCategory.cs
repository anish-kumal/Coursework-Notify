using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class MoodCategory
    {
        [PrimaryKey, AutoIncrement]
        public int MoodCategoryId { get; set; }

        [Required]
        public string? Name { get; set; } // Positive, Neutral, Negative

        public bool IsPredefined { get; set; }
    }
}
