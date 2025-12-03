using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity; // Để dùng IdentityUser nếu cần

namespace QuanLyRapPhim.Models
{
    public class UserVoucher
    {
        [Key]
        public int UserVoucherId { get; set; }

        [Required]
        public string UserId { get; set; } // FK tham chiếu đến dbo.AspNetUsers.Id (string GUID)

        [Required]
        public int VoucherId { get; set; } // FK tham chiếu đến Voucher.VoucherId

        public DateTime ClaimDate { get; set; } = DateTime.Now;

        public bool IsUsed { get; set; } = false;

        // Navigation properties (liên kết virtual)
        public virtual User User { get; set; } // ApplicationUser là class kế thừa IdentityUser của bạn
        public virtual Voucher Voucher { get; set; }
    }
}