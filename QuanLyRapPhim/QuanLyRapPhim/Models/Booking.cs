using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public int? ShowtimeId { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal? TotalPrice { get; set; } // Bao gồm cả vé + đồ ăn
        public decimal? FinalAmount { get; set; }
        public string? UserId { get; set; }
        public User? User { get; set; }
        public Showtime? Showtime { get; set; }
        public Payment? Payment { get; set; }
        public ICollection<BookingDetail>? BookingDetails { get; set; }
        public ICollection<BookingFood>? BookingFoods { get; set; } // Thêm

        // Trong Booking.cs, thêm sau các properties hiện có:
        public int? VoucherId { get; set; }
        public Voucher? Voucher { get; set; }

        public string? VoucherUsed { get; set; }
    }
}
