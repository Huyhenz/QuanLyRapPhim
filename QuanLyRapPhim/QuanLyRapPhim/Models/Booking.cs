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
        public Showtime Showtime { get; set; }
        public ICollection<BookingDetail> BookingDetails { get; set; }
    }
}
