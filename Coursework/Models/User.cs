using SQLite;
using System;
using System.ComponentModel.DataAnnotations;

namespace Coursework.Models
{
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int UserId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        // Stores 4-digit PIN hash
        [Required]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "PIN must be exactly 4 digits")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must be a 4-digit number")]
        public string PinHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
