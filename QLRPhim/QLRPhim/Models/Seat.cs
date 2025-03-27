namespace QLRPhim.Models
{
    public class Seat
    {
        public int SeatId { get; set; }
        public int RoomId { get; set; }
        public string SeatNumber { get; set; }
        public string Status { get; set; }
        public Room Room { get; set; }
        public ICollection<BookingDetail> BookingDetails { get; set; }
    }
}
