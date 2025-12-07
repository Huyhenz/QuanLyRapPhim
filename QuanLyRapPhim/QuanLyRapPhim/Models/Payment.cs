using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        public int BookingId { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; }

        // ✅ LƯU Ý: Luôn lưu bằng tiếng Anh trong DB
        // Giá trị: "Completed", "Pending", "Failed", "Cancelled"
        public string PaymentStatus { get; set; }

        public DateTime PaymentDate { get; set; }

        public Booking? Booking { get; set; }
    }
}