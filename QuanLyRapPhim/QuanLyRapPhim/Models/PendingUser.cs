using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class PendingUser
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string FullName { get; set; }
        
        [Required]
        public DateOnly DateOfBirth { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        [Required]
        public string PasswordHash { get; set; }
        
        [Required]
        public string ConfirmationToken { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? ExpiresAt { get; set; } // Token hết hạn sau 24 giờ
    }
}

