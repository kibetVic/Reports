using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Reports.Models
{
    public class UserAccount
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; }

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
        [StringLength(200)]
        public string FullName { get; set; }

        [Phone, StringLength(30)]
        public string PhoneNumber { get; set; }

        // Strongly typed role
        [Required]
        public UserRole Role { get; set; } = UserRole.User;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Navigation
        public ICollection<PaymentVoucher> CreatedVouchers { get; set; } = new List<PaymentVoucher>();
    }

    public enum UserRole
    {
        User = 0,
        Admin = 1,
        Manager = 2,
        Accountant = 3
    }
}
