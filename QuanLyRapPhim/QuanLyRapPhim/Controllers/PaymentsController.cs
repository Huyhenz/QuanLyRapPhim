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
using Microsoft.AspNetCore.Http; // Cho Session
using System.Text.Json; // Cho serialize


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

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy dữ liệu đặt vé tạm thời.";
                return RedirectToAction("Index", "Home");
            }

            // Load showtime nếu có (dùng AsNoTracking)
            Showtime showtime = null;
            if (tempBooking.ShowtimeId.HasValue)
            {
                showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .AsNoTracking() // Thêm AsNoTracking
                    .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId.Value);
            }

            // Load selected seats info (không lưu DB)
            var selectedSeats = new List<Seat>();
            if (tempBooking.SelectedSeatIds.Any() && showtime != null)
            {
                selectedSeats = await _context.Seats
                    .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                    .AsNoTracking()
                    .ToListAsync();
            }

            // Load selected foods
            var selectedFoods = new List<BookingFood>();
            foreach (var tf in tempBooking.SelectedFoods)
            {
                var food = await _context.FoodItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FoodItemId == tf.FoodItemId);

                if (food != null)
                {
                    selectedFoods.Add(new BookingFood
                    {
                        FoodItem = food,
                        Quantity = tf.Quantity,
                        UnitPrice = tf.UnitPrice
                    });
                }
            }

            // Fake Booking để hiển thị view (không lưu DB)
            var fakeBooking = new Booking
            {
                BookingId = 0, // Fake ID
                Showtime = showtime,
                BookingDate = DateTime.Now,
                TotalPrice = tempBooking.TotalPrice,
                FinalAmount = tempBooking.FinalAmount ?? tempBooking.TotalPrice,
                BookingDetails = tempBooking.SelectedSeatIds
                    .Select(id => new BookingDetail
                    {
                        SeatId = id,
                        Seat = selectedSeats.FirstOrDefault(s => s.SeatId == id)
                    })
                    .ToList(),
                BookingFoods = selectedFoods,
                VoucherUsed = tempBooking.VoucherUsed
            };

            // Truyền dữ liệu cho view
            ViewBag.Booking = fakeBooking;
            ViewBag.FullName = User.Identity?.Name ?? "Khách vãng lai";

            // Lấy danh sách vouchers của user
            var userId = _userManager.GetUserId(User);
            var userVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv =>
                    uv.UserId == userId &&
                    !uv.IsUsed &&
                    uv.Voucher.IsActive &&
                    uv.Voucher.ExpiryDate > DateTime.Now &&
                    uv.Voucher.UsedCount < uv.Voucher.UsageLimit)
                .Select(uv => uv.Voucher)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.UserVouchers = userVouchers;

            return View();
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
                return RedirectToAction("Create"); // Redirect back to payment page with error
            }

            // Lưu TempBooking vào TempData cho callback (vì redirect thanh toán có thể mất Session)
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking != null)
            {
                TempData["TempBookingJson"] = JsonSerializer.Serialize(tempBooking);
            }

            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
            return Redirect(url);
        }
        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            _logger.LogInformation("PaymentCallbackVnpay called at {Time}", DateTime.Now);

            var response = _vnPayService.PaymentExecute(Request.Query);

            _logger.LogInformation("VNPay Response: Success={Success}, OrderId={OrderId}, TransactionId={TransactionId}",
                response.Success, response.OrderId, response.TransactionId);

            // Load TempBooking từ TempData
            var tempBookingJson = TempData["TempBookingJson"] as string;
            if (string.IsNullOrEmpty(tempBookingJson))
            {
                _logger.LogError("Failed to retrieve TempBooking from TempData.");
                return Json(new { Success = false, Message = "Không tìm thấy dữ liệu đặt vé tạm thời." });
            }

            var tempBooking = JsonSerializer.Deserialize<TempBooking>(tempBookingJson);

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;

            if (!response.Success)
            {
                // Thanh toán thất bại: Xóa Session/TempData
                HttpContext.Session.Remove("TempBooking");
                TempData.Remove("TempBookingJson");
                return Json(new { Success = false, Message = "Thanh toán thất bại." });
            }

            // Thanh toán thành công: Tạo và lưu DB
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Load showtime nếu có (KHÔNG load Seats)
                Showtime showtime = null;
                int totalRows = 0;
                char lastRowLabel = 'A';

                if (tempBooking.ShowtimeId.HasValue)
                {
                    showtime = await _context.Showtimes
                        .Include(s => s.Room)
                        .Include(s => s.Movie)
                        .AsNoTracking() // QUAN TRỌNG: Không track
                        .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId.Value);

                    if (showtime == null)
                    {
                        throw new Exception("Không tìm thấy suất chiếu.");
                    }

                    // 2. Double-check ghế có còn available
                    var bookedSeats = await _context.BookingDetails
                        .Where(bd => bd.Booking.ShowtimeId == tempBooking.ShowtimeId)
                        .Select(bd => bd.SeatId)
                        .ToListAsync();

                    if (tempBooking.SelectedSeatIds.Any(s => bookedSeats.Contains(s)))
                    {
                        throw new Exception("Một hoặc nhiều ghế đã được đặt bởi người khác.");
                    }

                    // Tính số hàng cho pricing
                    var totalSeatsInRoom = await _context.Seats.CountAsync(s => s.RoomId == showtime.RoomId);
                    totalRows = (int)Math.Ceiling((double)totalSeatsInRoom / 10);
                    lastRowLabel = (char)('A' + totalRows - 1);
                }

                // 3. Tạo Booking entity
                var booking = new Booking
                {
                    ShowtimeId = tempBooking.ShowtimeId,
                    BookingDate = DateTime.Now,
                    UserId = userId,
                    TotalPrice = tempBooking.TotalPrice,
                    FinalAmount = tempBooking.FinalAmount ?? tempBooking.TotalPrice,
                    VoucherId = tempBooking.VoucherId,
                    VoucherUsed = tempBooking.VoucherUsed
                };

                // Thêm Booking trước để có BookingId
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // 4. Thêm BookingDetails (ghế) nếu có
                const decimal normalPrice = 50000;
                const decimal couplePrice = 100000;

                if (tempBooking.SelectedSeatIds.Any())
                {
                    // Load riêng seat info để tính giá
                    var seatInfos = await _context.Seats
                        .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                        .Select(s => new { s.SeatId, s.SeatNumber })
                        .AsNoTracking()
                        .ToListAsync();

                    foreach (var seatInfo in seatInfos)
                    {
                        decimal price = seatInfo.SeatNumber.StartsWith(lastRowLabel.ToString())
                            ? couplePrice
                            : normalPrice;

                        var bookingDetail = new BookingDetail
                        {
                            BookingId = booking.BookingId,
                            SeatId = seatInfo.SeatId,
                            Price = price
                        };

                        _context.BookingDetails.Add(bookingDetail);
                    }

                    await _context.SaveChangesAsync();

                    // 5. Cập nhật trạng thái ghế - Load riêng để update
                    var seatsToUpdate = await _context.Seats
                        .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                        .ToListAsync();

                    foreach (var seat in seatsToUpdate)
                    {
                        seat.Status = "Đã đặt";
                    }

                    await _context.SaveChangesAsync();
                }

                // 6. Thêm BookingFoods
                foreach (var tf in tempBooking.SelectedFoods)
                {
                    var bookingFood = new BookingFood
                    {
                        BookingId = booking.BookingId,
                        FoodItemId = tf.FoodItemId,
                        Quantity = tf.Quantity,
                        UnitPrice = tf.UnitPrice
                    };

                    _context.BookingFoods.Add(bookingFood);
                }

                await _context.SaveChangesAsync();

                // 7. Tạo Payment
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.FinalAmount ?? booking.TotalPrice ?? 0m,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VNPay",
                    PaymentStatus = "Completed"
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // 8. Cập nhật Voucher usage
                if (booking.VoucherId.HasValue && userId != null)
                {
                    var userVoucher = await _context.UserVouchers
                        .FirstOrDefaultAsync(uv =>
                            uv.UserId == userId &&
                            uv.VoucherId == booking.VoucherId &&
                            !uv.IsUsed);

                    if (userVoucher != null)
                    {
                        userVoucher.IsUsed = true;
                        _context.UserVouchers.Update(userVoucher);
                    }

                    var voucher = await _context.Vouchers.FindAsync(booking.VoucherId);
                    if (voucher != null)
                    {
                        voucher.UsedCount++;
                        _context.Vouchers.Update(voucher);
                    }

                    await _context.SaveChangesAsync();
                }

                // Commit transaction
                await transaction.CommitAsync();

                // Xóa Session/TempData
                HttpContext.Session.Remove("TempBooking");
                TempData.Remove("TempBookingJson");

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
                    Amount = booking.FinalAmount ?? booking.TotalPrice ?? 0m
                };

                return View(viewModel);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();

                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                _logger.LogError(dbEx, "Database error saving booking: {Message}", innerMsg);

                return Json(new
                {
                    Success = false,
                    Message = $"Lỗi cơ sở dữ liệu: {innerMsg}"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error saving booking in callback: {Message}", innerMsg);

                return Json(new
                {
                    Success = false,
                    Message = $"Lỗi khi lưu dữ liệu: {innerMsg}"
                });
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

        //[HttpGet]
        //public async Task<IActionResult> Create(int bookingId)
        //{
        //    var booking = await _context.Bookings
        //        .Include(b => b.Showtime).ThenInclude(s => s.Movie)
        //        .Include(b => b.Showtime)
        //        .ThenInclude(s => s.Room)
        //        .Include(b => b.BookingDetails)
        //        .ThenInclude(bd => bd.Seat)
        //        .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        //    ViewBag.Booking = booking;
        //    return View(new Payment { BookingId = bookingId });
        //}

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
        public async Task<IActionResult> ApplyVoucher(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá!" });

            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null)
                return Json(new { success = false, message = "Không tìm thấy dữ liệu tạm!" });

            if (tempBooking.TotalPrice <= 0)
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
                discount = tempBooking.TotalPrice * voucher.DiscountPercentage / 100;
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

            var newAmount = Math.Max(0, tempBooking.TotalPrice - discount);

            // Additional check: newAmount must >= 5000 to avoid payment error later
            if (newAmount < 5000 && newAmount > 0)
            {
                return Json(new { success = false, message = "Sau giảm giá, số tiền phải ít nhất 5.000 VNĐ để thanh toán!" });
            }

            // Apply voucher: Lưu VoucherId, VoucherUsed, và FinalAmount. IsUsed và UsedCount chỉ cập nhật khi thanh toán thành công.
            tempBooking.VoucherId = voucher.VoucherId;
            tempBooking.VoucherUsed = voucher.Code; // Lưu code cho hiển thị dễ dàng
            tempBooking.FinalAmount = newAmount;
            SaveTempBookingToSession(tempBooking);

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
        public IActionResult RemoveVoucher()
        {
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null)
                return Json(new { success = false, message = "Không tìm thấy dữ liệu tạm!" });

            tempBooking.VoucherId = null;
            tempBooking.VoucherUsed = null;
            tempBooking.FinalAmount = tempBooking.TotalPrice;

            SaveTempBookingToSession(tempBooking);

            return Json(new { success = true, message = "Đã hủy voucher!", newAmount = tempBooking.TotalPrice });
        }

        // Helper Session
        private TempBooking GetTempBookingFromSession()
        {
            var tempBookingJson = HttpContext.Session.GetString("TempBooking");
            return string.IsNullOrEmpty(tempBookingJson) ? null : JsonSerializer.Deserialize<TempBooking>(tempBookingJson);
        }

        private void SaveTempBookingToSession(TempBooking tempBooking)
        {
            HttpContext.Session.SetString("TempBooking", JsonSerializer.Serialize(tempBooking));
        }

    }
}