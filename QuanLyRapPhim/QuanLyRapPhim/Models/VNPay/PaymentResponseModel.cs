﻿namespace QuanLyRapPhim.Models.VNPay
{
    public class PaymentResponseModel
    {
        public string OrderDescription { get; set; }
        public string TransactionId { get; set; }
        public string OrderId { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentId { get; set; }
        public bool Success { get; set; }
        public string Token { get; set; }
        public string VnPayResponseCode { get; set; }
        public int BookingId { get; set; }
        public int ShowtimeId { get; set; }
        public DateTime BookingDate { get; set; }
        public string Title { get; set; }
        public List<int> SeatIds { get; set; }

    }
}
