using System.ComponentModel.DataAnnotations;
using System.Collections.Generic; // Để dùng ICollection

namespace QuanLyRapPhim.Models
{
    public class Voucher
    {
        [Key]
        public int VoucherId { get; set; }

        [Required(ErrorMessage = "Mã voucher là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Mã voucher không được vượt quá 50 ký tự.")]
        public string Code { get; set; } // Mã unique (ví dụ: "DISCOUNT20")

        public decimal DiscountAmount { get; set; } = 0; // Giảm giá cố định (VND), mặc định 0 nếu dùng %

        [Range(0, 100, ErrorMessage = "Phần trăm giảm giá phải từ 0 đến 100.")]
        public decimal DiscountPercentage { get; set; } = 0; // Giảm giá phần trăm, mặc định 0 nếu dùng cố định

        [Required(ErrorMessage = "Ngày hết hạn là bắt buộc.")]
        public DateTime ExpiryDate { get; set; } // Ngày hết hạn

        public bool IsActive { get; set; } = true; // Trạng thái (active/inactive)

        [Range(1, int.MaxValue, ErrorMessage = "Giới hạn sử dụng phải lớn hơn 0.")]
        public int UsageLimit { get; set; } = 1; // Giới hạn số lần dùng (mặc định 1)

        public int UsedCount { get; set; } = 0; // Số lần đã dùng

        // Liên kết one-to-many với Booking (một voucher áp dụng cho nhiều booking nếu cho phép)
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();
    }
}