using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Models.VNPay;
using QuanLyRapPhim.Service.Momo;
using QuanLyRapPhim.Service.VNPay;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage; // For transaction management
using System.Security.Claims; // For User.FindFirstValue

namespace QuanLyRapPhim.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly IMomoService _momoService;
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(DBContext context, UserManager<User> userManager, IMomoService momoService, IVnPayService vnPayService, ILogger<PaymentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _momoService = momoService;
            _vnPayService = vnPayService;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            _logger.LogInformation("CreatePaymentUrlVnpay called with BookingId: {BookingId}, Amount: {Amount}", model.BookingId, model.Amount);

            // Validation: Amount must be at least 5000 VND (VNPay requirement)
            if (model.Amount < 5000)
            {
                TempData["ErrorMessage"] = "Số tiền giao dịch phải ít nhất 5.000 VNĐ.";
                _logger.LogWarning("Invalid amount: {Amount} for BookingId: {BookingId}", model.Amount, model.BookingId);
                return RedirectToAction("Create", new { bookingId = model.BookingId }); // Redirect back to payment page with error
            }

            // Store BookingId in TempData with explicit type conversion
            TempData["BookingId"] = model.BookingId.ToString();
            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            _logger.LogInformation("PaymentCallbackVnpay called at {Time}", DateTime.Now);

            var response = _vnPayService.PaymentExecute(Request.Query);

            _logger.LogInformation("VNPay Response: Success={Success}, OrderId={OrderId}, TransactionId={TransactionId}", response.Success, response.OrderId, response.TransactionId);

            // Retrieve BookingId from TempData
            string bookingIdString = TempData["BookingId"]?.ToString();
            if (string.IsNullOrEmpty(bookingIdString) || !int.TryParse(bookingIdString, out int bookingId))
            {
                _logger.LogError("Failed to retrieve or parse BookingId from TempData. TempData['BookingId']={BookingIdString}", bookingIdString);
                return Json(new { Success = false, Message = "Không tìm thấy hoặc không thể phân tích BookingId." });
            }

            _logger.LogInformation("Retrieved BookingId: {BookingId}", bookingId);

            // Find the booking
            var booking = await _context.Bookings
                .Include(b => b.Voucher) // Luôn load voucher để cập nhật UsedCount nếu cần
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null)
            {
                _logger.LogError("Booking not found for BookingId: {BookingId}", bookingId);
                return Json(new { Success = false, Message = "Không tìm thấy thông tin đặt vé." });
            }

            _logger.LogInformation("Booking found: BookingId={BookingId}, TotalPrice={TotalPrice}", booking.BookingId, booking.TotalPrice);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Check or create Payment record
                    var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
                    decimal actualAmount = response.Amount / 100; // Adjust based on VNPay (assuming multiplied by 100)
                    if (payment == null)
                    {
                        payment = new Payment
                        {
                            BookingId = bookingId,
                            Amount = actualAmount,
                            PaymentMethod = "VNPay",
                            PaymentDate = DateTime.Now,
                            PaymentStatus = response.Success ? "Completed" : "Failed"
                        };
                        _context.Payments.Add(payment);
                        _logger.LogInformation("Created new Payment record for BookingId: {BookingId}, Status: {PaymentStatus}", payment.BookingId, payment.PaymentStatus);
                    }
                    else
                    {
                        payment.Amount = actualAmount;
                        payment.PaymentStatus = response.Success ? "Completed" : "Failed";
                        _context.Payments.Update(payment);
                        _logger.LogInformation("Updated Payment record for BookingId: {BookingId}, New Status: {PaymentStatus}", payment.BookingId, payment.PaymentStatus);
                    }

                    if (response.Success)
                    {
                        // Sync booking.FinalAmount to match the actual paid amount from VNPay
                        booking.FinalAmount = actualAmount;
                        _context.Bookings.Update(booking);

                        // If voucher was applied, mark UserVoucher as used and increment Voucher.UsedCount
                        if (booking.VoucherId.HasValue)
                        {
                            var userId = booking.UserId; // Assume UserId in booking
                            var userVoucher = await _context.UserVouchers
                                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == booking.VoucherId && !uv.IsUsed);

                            if (userVoucher != null)
                            {
                                userVoucher.IsUsed = true;
                                _context.UserVouchers.Update(userVoucher);

                                var voucher = booking.Voucher;
                                if (voucher != null)
                                {
                                    voucher.UsedCount++;
                                    _context.Vouchers.Update(voucher);
                                }
                            }
                        }

                        // No longer need this check since we sync to actualAmount
                        // if (booking.FinalAmount == 0 || booking.FinalAmount == null)
                        // {
                        //     booking.FinalAmount = booking.TotalPrice;
                        // }
                    }
                    else
                    {
                        // On failure, optionally remove voucher application to allow retry
                        booking.VoucherId = null;
                        booking.VoucherUsed = null;
                        booking.FinalAmount = booking.TotalPrice;
                        _context.Bookings.Update(booking);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Prepare model for View
                    var viewModel = new PaymentResponseModel
                    {
                        Success = response.Success,
                        TransactionId = response.TransactionId,
                        OrderId = response.OrderId,
                        PaymentMethod = response.PaymentMethod ?? "VNPay",
                        OrderDescription = response.OrderDescription,
                        VnPayResponseCode = response.VnPayResponseCode,
                        Token = response.Token,
                        Amount = booking.FinalAmount // Use the synced FinalAmount
                    };

                    // Pass BookingId to ViewBag if needed for script
                    ViewBag.BookingId = bookingId;

                    return View(viewModel);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error in PaymentCallbackVnpay for BookingId: {BookingId}", bookingId);
                    return Json(new { Success = false, Message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }

        [HttpPost]
        [Route("CreatePaymentUrl")]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model)
        {
            var response = await _momoService.CreatePaymentMomo(model);
            return Redirect(response.PayUrl);
        }

        // GET: Payments
        public async Task<IActionResult> Index()
        {
            var dBContext = _context.Payments.Include(p => p.Booking);
            return View(await dBContext.ToListAsync());
        }

        // GET: Payments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            ViewBag.Booking = booking;
            return View(new Payment { BookingId = bookingId });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return NotFound();
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId", payment.BookingId);
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentId,BookingId,Amount,PaymentMethod,PaymentStatus,PaymentDate")] Payment payment)
        {
            if (id != payment.PaymentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(payment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentExists(payment.PaymentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId", payment.BookingId);
            return View(payment);
        }

        public async Task<IActionResult> Bill(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(p => p.Booking)
                .ThenInclude(b => b.Showtime)
                .ThenInclude(s => s.Room)
                .Include(p => p.Booking)
                .ThenInclude(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.FullName = user.FullName;
                ViewBag.UserEmail = user.Email;
                ViewBag.DateOfBirth = user.DateOfBirth.ToString("dd/MM/yyyy");
            }
            else
            {
                ViewBag.FullName = "Khách vãng lai";
                ViewBag.UserEmail = "Không có";
                ViewBag.DateOfBirth = "Không có";
            }

            return View(payment);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                _context.Payments.Remove(payment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.PaymentId == id);
        }

        public IActionResult PaymentHistory()
        {
            var userId = _userManager.GetUserId(User); // Lấy ID người dùng đang đăng nhập

            var bookings = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Showtime).ThenInclude(st => st.Movie)
                .Include(b => b.Showtime).ThenInclude(st => st.Room)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Where(b => b.UserId == userId)
                .ToList();

            var payments = _context.Payments
                .Where(p => p.PaymentStatus == "Đã thanh toán")
                .ToList();

            // Lọc các booking đã thanh toán
            var paidBookingIds = payments.Select(p => p.BookingId).ToHashSet();
            bookings = bookings.Where(b => paidBookingIds.Contains(b.BookingId)).ToList();

            ViewBag.Payments = payments;

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyVoucher(string code, int bookingId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá!" });

            var booking = await _context.Bookings
                .Include(b => b.Voucher) // Load voucher nếu cần
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

            if (booking.TotalPrice <= 0)
                return Json(new { success = false, message = "Đơn hàng không có giá trị để áp dụng giảm giá!" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Bạn cần đăng nhập để sử dụng voucher!" });

            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v =>
                    v.Code.Trim().ToUpper() == code.Trim().ToUpper() &&
                    v.IsActive &&
                    v.ExpiryDate >= DateTime.Today &&
                    (v.UsageLimit == 0 || v.UsedCount < v.UsageLimit));

            if (voucher == null)
                return Json(new { success = false, message = "Mã giảm giá không hợp lệ, đã hết hạn hoặc đã dùng hết!" });

            // Kiểm tra xem người dùng đã claim voucher này và chưa sử dụng
            var userVoucher = await _context.UserVouchers
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == voucher.VoucherId && !uv.IsUsed);

            if (userVoucher == null)
                return Json(new { success = false, message = "Bạn chưa claim voucher này hoặc đã sử dụng!" });

            decimal discount = 0;
            string discountType = "";

            if (voucher.DiscountPercentage > 0)
            {
                discount = booking.TotalPrice * voucher.DiscountPercentage / 100;
                discountType = "Percentage";
            }
            else if (voucher.DiscountAmount > 0)
            {
                discount = voucher.DiscountAmount;
                discountType = "Fixed";
            }
            else
            {
                return Json(new { success = false, message = "Voucher không có giá trị giảm giá!" });
            }

            var newAmount = Math.Max(0, booking.TotalPrice - discount);

            // Additional check: newAmount must >= 5000 to avoid payment error later
            if (newAmount < 5000 && newAmount > 0)
            {
                return Json(new { success = false, message = "Sau giảm giá, số tiền phải ít nhất 5.000 VNĐ để thanh toán!" });
            }

            // Apply voucher: Lưu VoucherId, VoucherUsed (code), và FinalAmount. IsUsed và UsedCount chỉ cập nhật khi thanh toán thành công.
            booking.VoucherId = voucher.VoucherId;
            booking.VoucherUsed = voucher.Code; // Lưu code cho hiển thị dễ dàng
            booking.FinalAmount = newAmount;
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                voucher = new
                {
                    Code = voucher.Code,
                    DiscountPercentage = voucher.DiscountPercentage,
                    DiscountAmount = voucher.DiscountAmount,
                    DiscountType = discountType,
                    DiscountValue = discountType == "Percentage" ? voucher.DiscountPercentage : voucher.DiscountAmount
                },
                discount = discount,
                newAmount = newAmount,
                message = discountType == "Percentage"
                    ? $"Giảm {voucher.DiscountPercentage}%"
                    : $"Giảm {voucher.DiscountAmount:N0}đ"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveVoucher(int bookingId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

            // Hủy voucher: Reset VoucherId, VoucherUsed, và FinalAmount = TotalPrice
            booking.VoucherId = null;
            booking.VoucherUsed = null;
            booking.FinalAmount = booking.TotalPrice;
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã hủy voucher!", newAmount = booking.TotalPrice });
        }

    }
}