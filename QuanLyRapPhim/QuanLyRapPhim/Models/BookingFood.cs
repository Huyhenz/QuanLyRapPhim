using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models
{
    public class BookingFood
    {
        [Key]
        public int BookingFoodId { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; }

        public int FoodItemId { get; set; }
        public FoodItem FoodItem { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Giá tại thời điểm đặt
    }
}
