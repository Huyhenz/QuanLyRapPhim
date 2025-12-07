using Microsoft.AspNetCore.Mvc;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Service;
using System.Threading.Tasks;
using QuanLyRapPhim.Service.VNPay;
using System.Text.RegularExpressions;
using System.Web;

namespace LearningManagementSystem.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly IVnPayService _vnPayService;

        public CheckoutController(IVnPayService vnPayService)
        {
            _vnPayService = vnPayService;
        }

        //[HttpGet]
        //public IActionResult PaymentCallbackVnpay()
        //{
        //    // Lấy response từ VNPay service
        //    var response = _vnPayService.PaymentExecute(Request.Query);

        //    // ===== FIX: Tách số tiền từ vnp_OrderInfo hoặc vnp_Amount =====
        //    decimal amount = 0;

        //    // Cách 1: Lấy từ vnp_OrderInfo (ưu tiên)
        //    var orderInfo = Request.Query["vnp_OrderInfo"].ToString();
        //    if (!string.IsNullOrEmpty(orderInfo))
        //    {
        //        // Decode URL: "tudomb2002%40gmail.com+Thanh+to%C3%A1n+VNPay+140000"
        //        var decoded = HttpUtility.UrlDecode(orderInfo);
                
        //        // Tìm số ở cuối chuỗi: "tudomb2002@gmail.com Thanh toán VNPay 140000"
        //        var match = Regex.Match(decoded, @"(\d+)$");
        //        if (match.Success)
        //        {
        //            amount = decimal.Parse(match.Groups[1].Value);
        //        }
        //    }

        //    // Cách 2: Nếu không tìm thấy, lấy từ vnp_Amount (fallback)
        //    if (amount == 0)
        //    {
        //        var vnpAmount = Request.Query["vnp_Amount"].ToString();
        //        if (!string.IsNullOrEmpty(vnpAmount) && decimal.TryParse(vnpAmount, out var parsedAmount))
        //        {
        //            // vnp_Amount có đơn vị là đồng * 100
        //            amount = parsedAmount / 100;
        //        }
        //    }

        //    // Set Amount vào response model
        //    response.Amount = amount;

        //    // Set ViewBag nếu cần (cho view sử dụng)
        //    ViewBag.BookingId = 0; // Nếu có BookingId thì set ở đây

        //    return View(response);
        //}
    }
}