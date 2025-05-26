using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Movie
    {
        [Key]
        public int MovieId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Duration { get; set; }
        [Url]
        public string Poster { get; set; }
        public string Genre { get; set; }
        public string Director { get; set; }
        public string Actors { get; set; }
        public string TrailerUrl { get; set; }

        public ICollection<Showtime>? Showtimes { get; set; }
        public List<Review> Reviews { get; set; } = new List<Review>();
    }
}
