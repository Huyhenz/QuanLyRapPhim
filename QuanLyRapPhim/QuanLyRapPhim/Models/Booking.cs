using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public int ShowtimeId { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string? UserId { get; set; } // Thêm trường UserId
        public User? User { get; set; } // Thêm mối quan hệ với User
        public Showtime? Showtime { get; set; }
        public ICollection<BookingDetail>? BookingDetails { get; set; }
    }
}
