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

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CreatePaymentVnPay(string courseId)
        //{
        //    var course = await _courseRepo.GetCourseByIdAsync(courseId);
        //    if (course == null)
        //    {
        //        TempData["Error"] = "Không tìm thấy khóa học.";
        //        return RedirectToAction("Index", "Home");
        //    }

        //    // Tạo link thanh toán VNPay
        //    string paymentUrl = GenerateVnPayUrl(course);

        //    return Redirect(paymentUrl);
        //}



        //private string GenerateVnPayUrl(Course course)
        //{
        //    string baseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        //    return $"{baseUrl}?amount={course.Price}&orderInfo=KhoaHoc_{course.CourseId}";
        //}


        [HttpGet]
        public IActionResult PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);
            return View(response);
        }



        // Thêm các action để xử lý kết quả
        //public IActionResult PaymentSuccess(string transactionId)
        //{
        //    ViewBag.TransactionId = transactionId;
        //    return View();
        //}

        //public IActionResult PaymentFailed(string error)
        //{
        //    ViewBag.Error = error;
        //    return View();
        //}
    }
}
