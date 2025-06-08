using Microsoft.AspNetCore.Mvc;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Service; // nếu có lớp xử lý thanh toán tách riêng
using System.Threading.Tasks;
using QuanLyRapPhim.Service.VNPay;

namespace LearningManagementSystem.Controllers
{
    public class CheckoutController : Controller
    {
        //private readonly ICourseRepository _courseRepo;
        private readonly IVnPayService _vnPayService;

        public CheckoutController(IVnPayService vnPayService)
        {
            _vnPayService = vnPayService;
        }

        [HttpGet]
        public IActionResult PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);
            return View(response);
        }
    }
}
