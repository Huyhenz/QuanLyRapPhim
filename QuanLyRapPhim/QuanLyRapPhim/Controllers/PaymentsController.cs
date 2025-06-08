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
using QuanLyRapPhim.Service.VNPay;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage; // For transaction management

namespace QuanLyRapPhim.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(DBContext context, UserManager<User> userManager, IVnPayService vnPayService, ILogger<PaymentsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _vnPayService = vnPayService;
            _logger = logger;
        }

        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            _logger.LogInformation("CreatePaymentUrlVnpay called with BookingId: {BookingId}, Amount: {Amount}", model.BookingId, model.Amount);

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
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null)
            {
                _logger.LogError("Booking not found for BookingId: {BookingId}", bookingId);
                return Json(new { Success = false, Message = "Không tìm thấy thông tin đặt vé." });
            }

            _logger.LogInformation("Booking found: BookingId={BookingId}, TotalPrice={TotalPrice}", booking.BookingId, booking.TotalPrice);

            // Retrieve SeatIds from BookingDetails
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.BookingId == bookingId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            _logger.LogInformation("Retrieved {SeatCount} SeatIds for BookingId: {BookingId}", bookingDetails.Count, bookingId);

            // Store BookingId and SeatIds in ViewBag
            ViewBag.BookingId = bookingId;
            ViewBag.SeatIds = bookingDetails;

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Check or create Payment record
                    var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
                    if (payment == null)
                    {
                        payment = new Payment
                        {
                            BookingId = bookingId,
                            Amount = booking.TotalPrice,
                            PaymentMethod = "VNPay",
                            PaymentDate = DateTime.Now,
                            PaymentStatus = response.Success ? "Completed" : "Failed"
                        };
                        _context.Payments.Add(payment);
                        _logger.LogInformation("Created new Payment record for BookingId: {BookingId}, Status: {PaymentStatus}", payment.BookingId, payment.PaymentStatus);
                    }
                    else
                    {
                        payment.PaymentStatus = response.Success ? "Completed" : "Failed";
                        payment.PaymentDate = DateTime.Now;
                        _context.Payments.Update(payment);
                        _logger.LogInformation("Updated existing Payment record for BookingId: {BookingId}, Status: {PaymentStatus}", payment.BookingId, payment.PaymentStatus);
                    }

                    // Save changes within the transaction
                    int changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Database changes saved: {Changes} record(s) affected", changes);

                    // Commit the transaction
                    await transaction.CommitAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database update failed for BookingId: {BookingId}. Inner Exception: {InnerException}", bookingId, ex.InnerException?.Message);
                    await transaction.RollbackAsync();
                    return Json(new { Success = false, Message = "Lỗi khi lưu thông tin thanh toán vào cơ sở dữ liệu." });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error for BookingId: {BookingId}", bookingId);
                    await transaction.RollbackAsync();
                    return Json(new { Success = false, Message = "Lỗi không mong muốn khi xử lý thanh toán." });
                }
            }

            // Pass the response to the VNPayResponse view
            return View("PaymentCallbackVnpay", response);
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

        public async Task<IActionResult> Create(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                return NotFound();
            }

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalPrice,
                PaymentDate = DateTime.Now,
                PaymentStatus = "Pending",
                PaymentMethod = "Cash"
            };

            ViewBag.Booking = booking;
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentId,BookingId,Amount,PaymentMethod,PaymentStatus,PaymentDate")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                payment.PaymentStatus = "Completed";
                _context.Add(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thanh toán thành công!";
                return RedirectToAction("Bill", new { paymentId = payment.PaymentId });
            }

            var booking = await _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == payment.BookingId);

            ViewBag.Booking = booking;
            return View(payment);
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

    }
}