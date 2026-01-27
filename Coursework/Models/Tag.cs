using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace MauiApp1.Models
{
    public class Tag
    {
        [PrimaryKey, AutoIncrement]
        public int TagId { get; set; }

        [Required]
        public string? TagName { get; set; }

        public bool IsPredefined { get; set; }


    }
}
