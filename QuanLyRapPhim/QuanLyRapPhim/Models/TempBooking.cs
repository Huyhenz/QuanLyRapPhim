using System.Collections.Generic;
using System.Text.Json.Serialization; // Để serialize dễ dàng

namespace QuanLyRapPhim.Models
{
    public class TempBooking
    {
        public int? ShowtimeId { get; set; }
        public List<int> SelectedSeatIds { get; set; } = new List<int>(); // Danh sách SeatId đã chọn
        public List<TempFoodItem> SelectedFoods { get; set; } = new List<TempFoodItem>(); // Danh sách thức ăn
        public int? VoucherId { get; set; } // Voucher tạm (nếu áp dụng)
        public decimal TotalPrice { get; set; } // Tính tạm thời
        public decimal? FinalAmount { get; set; } // Sau voucher
        public string VoucherUsed { get; set; } // Code voucher đã dùng

        // Không lưu UserId ở đây, vì sẽ lấy từ User hiện tại khi lưu thực tế
    }

    public class TempFoodItem
    {
        public int FoodItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}