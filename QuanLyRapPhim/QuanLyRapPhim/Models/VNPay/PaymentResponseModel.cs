namespace QuanLyRapPhim.Models.VNPay
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

        /// <summary>
        /// Mã phản hồi từ VNPAY (00 = success, 24 = cancelled, etc.)
        /// </summary>
        public string VnPayResponseCode { get; set; }

        /// <summary>
        /// Trạng thái giao dịch từ VNPAY (00 = success, 02 = failed, etc.)
        /// </summary>
        public string TransactionStatus { get; set; }

        public decimal Amount { get; set; }
    }
}