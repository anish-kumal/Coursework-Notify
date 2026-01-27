using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class JournalTag
    {
        [PrimaryKey, AutoIncrement]
        public int JournalTagId { get; set; }

        [Required]
        public int JournalId { get; set; }

        [Required]
        public int TagId { get; set; }
    }
}
