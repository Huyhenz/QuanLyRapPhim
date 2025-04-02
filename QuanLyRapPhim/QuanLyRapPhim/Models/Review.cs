using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class Review
    {
        public int ReviewId { get; set; }

        [Required(ErrorMessage = "Nội dung nhận xét là bắt buộc.")]
        [StringLength(500, ErrorMessage = "Nội dung nhận xét không được vượt quá 500 ký tự.")]
        public string Comment { get; set; }

        [Required(ErrorMessage = "Điểm đánh giá là bắt buộc.")]
        [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
        public int Rating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Liên kết với phim
        public int MovieId { get; set; }
        public Movie Movie { get; set; }

        // Liên kết với người dùng
        public string UserId { get; set; }
        public User User { get; set; }
    }
}
