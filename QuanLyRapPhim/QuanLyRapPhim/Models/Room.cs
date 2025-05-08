using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Room
    {
        [Required]
        [Key]
        public int RoomId { get; set; }
        public string RoomName { get; set; }

        public int Capacity { get; set; }
        public ICollection<Seat>? Seats { get; set; }
        public ICollection<Showtime>? Showtimes { get; set; }

    }
}
