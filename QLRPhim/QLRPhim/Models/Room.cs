using System.ComponentModel.DataAnnotations;

namespace QLRPhim.Models
{
    public class Room
    {
        [Key]
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int Capacity { get; set; }
        public ICollection<Seat> Seats { get; set; }
    }
}
