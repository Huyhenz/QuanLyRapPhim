using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Showtime
    {
        [Key]
        [Required]
        public int ShowtimeId { get; set; }
        public int MovieId { get; set; }
        public string Poster { get; set; }
        public string StartTime { get; set; }
        public DateTime Date { get; set; }

        public Movie Movie { get; set; }
        public ICollection<Booking> Bookings { get; set; }
    }
}
