using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Showtime
    {
        [Key]
        public int ShowtimeId { get; set; }
        public string Title { get; set; } 
        public int MovieId { get; set; }
        [Url]
        public string Poster { get; set; }
        public string StartTime { get; set; }
        public DateTime Date { get; set; }
        public int RoomId { get; set; }
        public Room? Room { get; set; }
        public Movie? Movie { get; set; }
        public ICollection<Booking>? Bookings { get; set; }
    }
}
