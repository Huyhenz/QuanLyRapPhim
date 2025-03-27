using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Movie
    {
        [Key]
        [Required]
        public int MovieId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Duration { get; set; }
        public string Poster { get; set; }
        public string Genre { get; set; }
        public string Director { get; set; }
        public string Actors { get; set; }

        public ICollection<Showtime> Showtimes { get; set; }
    }
}
