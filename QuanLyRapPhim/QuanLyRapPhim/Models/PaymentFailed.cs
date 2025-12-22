using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyRapPhim.Models
{
    public class PaymentFailed
    {
        [Key]
        public int PaymentFailedId { get; set; }
        
        public string? TransactionId { get; set; }
        public string? OrderId { get; set; }
        public string? Email { get; set; }
        public string? UserId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? FailureReason { get; set; }
        public string? VnPayResponseCode { get; set; }
        public DateTime FailedDate { get; set; } = DateTime.Now;
        
        // Thông tin booking
        public int? ShowtimeId { get; set; }
        public string? MovieTitle { get; set; }
        public string? RoomName { get; set; }
        public DateTime? ShowDate { get; set; }
        public TimeSpan? ShowTime { get; set; }
        
        // Lưu danh sách ghế dưới dạng JSON string
        public string? SelectedSeatsJson { get; set; }
        
        // Lưu danh sách đồ ăn dưới dạng JSON string
        public string? SelectedFoodsJson { get; set; }
        
        public string? VoucherUsed { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? FinalAmount { get; set; }
        
        // Navigation properties
        [NotMapped]
        public List<string> SelectedSeats
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedSeatsJson))
                    return new List<string>();
                
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(SelectedSeatsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set => SelectedSeatsJson = value != null 
                ? System.Text.Json.JsonSerializer.Serialize(value) 
                : null;
        }
        
        [NotMapped]
        public List<FailedFoodItem> SelectedFoods
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedFoodsJson))
                    return new List<FailedFoodItem>();
                
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<FailedFoodItem>>(SelectedFoodsJson) ?? new List<FailedFoodItem>();
                }
                catch
                {
                    return new List<FailedFoodItem>();
                }
            }
            set => SelectedFoodsJson = value != null 
                ? System.Text.Json.JsonSerializer.Serialize(value) 
                : null;
        }
    }

    public class FailedFoodItem
    {
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

