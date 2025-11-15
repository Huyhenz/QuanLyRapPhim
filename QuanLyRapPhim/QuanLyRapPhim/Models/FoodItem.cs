using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class FoodItem
    {
        [Key]
        public int FoodItemId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public decimal Price { get; set; }

        [StringLength(50)]
        public string Size { get; set; } // Small, Medium, Large, Combo

        public string? ImageUrl { get; set; }

        public string Category { get; set; } // Popcorn, Drink, Combo
    }
}
