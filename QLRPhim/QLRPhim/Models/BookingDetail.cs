using System.ComponentModel.DataAnnotations;

namespace QLRPhim.Models
{
    public class BookingDetail
    {
        [Key]
        public int BookingDetailId { get; set; }
        public int BookingId { get; set; }
        public int SeatId { get; set; }
        public decimal Price { get; set; }
        public Booking Booking { get; set; }
        public Seat Seat { get; set; }
    }
}
