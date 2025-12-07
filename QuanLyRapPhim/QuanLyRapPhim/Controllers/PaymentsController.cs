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
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

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

            Showtime showtime = null;
            if (tempBooking.ShowtimeId.HasValue)
            {
                showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId.Value);
            }

            var selectedSeats = new List<Seat>();
            if (tempBooking.SelectedSeatIds.Any() && showtime != null)
            {
                selectedSeats = await _context.Seats
                    .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                    .AsNoTracking()
                    .ToListAsync();
            }

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

            var fakeBooking = new Booking
            {
                BookingId = 0,
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

            ViewBag.Booking = fakeBooking;
            ViewBag.FullName = User.Identity?.Name ?? "Khách vãng lai";

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

            if (model.Amount < 5000)
            {
                TempData["ErrorMessage"] = "Số tiền giao dịch phải ít nhất 5.000 VNĐ.";
                _logger.LogWarning("Invalid amount: {Amount} for BookingId: {BookingId}", model.Amount, model.BookingId);
                return RedirectToAction("Create");
            }

            var tempBooking = GetTempBookingFromSession();
            if (tempBooking != null)
            {
                TempData["TempBookingJson"] = JsonSerializer.Serialize(tempBooking);
                _logger.LogInformation("TempBooking saved to TempData: TotalPrice={TotalPrice}, FinalAmount={FinalAmount}",
                    tempBooking.TotalPrice, tempBooking.FinalAmount);
            }

            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            _logger.LogInformation("=== PaymentCallbackVnpay START ===");
            _logger.LogInformation("Callback received at {Time}", DateTime.Now);

            var response = _vnPayService.PaymentExecute(Request.Query);

            _logger.LogInformation("VNPay Response: Success={Success}, OrderId={OrderId}, TransactionId={TransactionId}, ResponseCode={ResponseCode}",
                response.Success, response.OrderId, response.TransactionId, response.VnPayResponseCode);

            var tempBookingJson = TempData["TempBookingJson"] as string;
            if (string.IsNullOrEmpty(tempBookingJson))
            {
                _logger.LogError("CRITICAL: Failed to retrieve TempBooking from TempData");
                TempData["ErrorMessage"] = "Không tìm thấy dữ liệu đặt vé tạm thời.";
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation("TempBooking retrieved from TempData");

            TempBooking tempBooking;
            try
            {
                tempBooking = JsonSerializer.Deserialize<TempBooking>(tempBookingJson);
                _logger.LogInformation("TempBooking deserialized: ShowtimeId={ShowtimeId}, TotalPrice={TotalPrice}, FinalAmount={FinalAmount}, SelectedSeats={SeatCount}",
                    tempBooking.ShowtimeId, tempBooking.TotalPrice, tempBooking.FinalAmount, tempBooking.SelectedSeatIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize TempBooking");
                TempData["ErrorMessage"] = "Lỗi xử lý dữ liệu đặt vé.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;

            _logger.LogInformation("User: {UserId}, Email: {Email}", userId ?? "NULL", user?.Email ?? "NULL");

            // ===== THANH TOÁN THẤT BẠI =====
            if (!response.Success)
            {
                _logger.LogWarning("Payment FAILED - ResponseCode: {ResponseCode}", response.VnPayResponseCode);

                HttpContext.Session.Remove("TempBooking");
                TempData.Remove("TempBookingJson");

                var failedModel = new PaymentResponseModel
                {
                    Success = false,
                    TransactionId = response.TransactionId ?? "N/A",
                    OrderId = response.OrderId ?? "N/A",
                    PaymentMethod = response.PaymentMethod ?? "VNPay",
                    OrderDescription = user?.Email ?? "Unknown",
                    VnPayResponseCode = response.VnPayResponseCode,
                    Amount = tempBooking.FinalAmount ?? tempBooking.TotalPrice
                };

                _logger.LogInformation("Returning failed view with Amount: {Amount}", failedModel.Amount);

                ViewBag.BookingId = 0;
                return View(failedModel);
            }

            // ===== THANH TOÁN THÀNH CÔNG =====
            _logger.LogInformation("Payment SUCCESS - Starting database transaction");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Load showtime
                Showtime showtime = null;
                int totalRows = 0;
                char lastRowLabel = 'A';

                if (tempBooking.ShowtimeId.HasValue)
                {
                    _logger.LogInformation("Loading showtime: {ShowtimeId}", tempBooking.ShowtimeId.Value);

                    showtime = await _context.Showtimes
                        .Include(s => s.Room)
                        .Include(s => s.Movie)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId.Value);

                    if (showtime == null)
                    {
                        _logger.LogError("Showtime not found: {ShowtimeId}", tempBooking.ShowtimeId.Value);
                        throw new Exception($"Không tìm thấy suất chiếu ID {tempBooking.ShowtimeId.Value}");
                    }

                    _logger.LogInformation("Showtime loaded: Movie={MovieTitle}, Room={RoomName}",
                        showtime.Movie?.Title ?? "N/A", showtime.Room?.RoomName ?? "N/A");

                    // 2. Double-check ghế có còn available
                    _logger.LogInformation("Checking seat availability for {Count} seats", tempBooking.SelectedSeatIds.Count);

                    var bookedSeats = await _context.BookingDetails
                        .Where(bd => bd.Booking.ShowtimeId == tempBooking.ShowtimeId)
                        .Select(bd => bd.SeatId)
                        .ToListAsync();

                    var conflictSeats = tempBooking.SelectedSeatIds.Where(s => bookedSeats.Contains(s)).ToList();
                    if (conflictSeats.Any())
                    {
                        _logger.LogError("Seats already booked: {Seats}", string.Join(", ", conflictSeats));
                        throw new Exception($"Ghế {string.Join(", ", conflictSeats)} đã được đặt bởi người khác.");
                    }

                    _logger.LogInformation("Seat availability check PASSED");

                    var totalSeatsInRoom = await _context.Seats.CountAsync(s => s.RoomId == showtime.RoomId);
                    totalRows = (int)Math.Ceiling((double)totalSeatsInRoom / 10);
                    lastRowLabel = (char)('A' + totalRows - 1);

                    _logger.LogInformation("Room has {TotalRows} rows, last row: {LastRow}", totalRows, lastRowLabel);
                }

                // 3. Tạo Booking entity
                _logger.LogInformation("Creating Booking entity");

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

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Booking created successfully - BookingId: {BookingId}", booking.BookingId);

                // 4. Thêm BookingDetails (ghế)
                const decimal normalPrice = 50000;
                const decimal couplePrice = 100000;

                if (tempBooking.SelectedSeatIds.Any())
                {
                    _logger.LogInformation("Creating BookingDetails for {Count} seats", tempBooking.SelectedSeatIds.Count);

                    var seatInfos = await _context.Seats
                        .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                        .Select(s => new { s.SeatId, s.SeatNumber })
                        .AsNoTracking()
                        .ToListAsync();

                    if (seatInfos.Count != tempBooking.SelectedSeatIds.Count)
                    {
                        _logger.LogError("Seat count mismatch: Expected {Expected}, Found {Found}",
                            tempBooking.SelectedSeatIds.Count, seatInfos.Count);
                        throw new Exception("Không tìm thấy đầy đủ thông tin ghế.");
                    }

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

                        _logger.LogInformation("Added BookingDetail: SeatId={SeatId}, SeatNumber={SeatNumber}, Price={Price}",
                            seatInfo.SeatId, seatInfo.SeatNumber, price);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ {Count} BookingDetails saved successfully", seatInfos.Count);

                    // 5. Cập nhật trạng thái ghế
                    _logger.LogInformation("Updating seat statuses to 'Đã đặt'");

                    var seatsToUpdate = await _context.Seats
                        .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                        .ToListAsync();

                    foreach (var seat in seatsToUpdate)
                    {
                        _logger.LogInformation("Updating Seat {SeatId} ({SeatNumber}): '{OldStatus}' -> 'Đã đặt'",
                            seat.SeatId, seat.SeatNumber, seat.Status);
                        seat.Status = "Đã đặt";
                    }

                    _context.Seats.UpdateRange(seatsToUpdate);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ {Count} seat statuses updated successfully", seatsToUpdate.Count);
                }

                // 6. Thêm BookingFoods
                if (tempBooking.SelectedFoods.Any())
                {
                    _logger.LogInformation("Creating BookingFoods for {Count} items", tempBooking.SelectedFoods.Count);

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

                        _logger.LogInformation("Added BookingFood: FoodItemId={FoodItemId}, Quantity={Quantity}, UnitPrice={UnitPrice}",
                            tf.FoodItemId, tf.Quantity, tf.UnitPrice);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ {Count} BookingFoods saved successfully", tempBooking.SelectedFoods.Count);
                }

                // 7. ✅ FIX: Tạo Payment với PaymentStatus = PaymentStatus.Completed (English constant)
                _logger.LogInformation("Creating Payment record");

                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.FinalAmount ?? booking.TotalPrice ?? 0m,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VNPay",
                    PaymentStatus = PaymentStatus.Completed // ✅ FIX: Use constant "Completed" instead of "Đã thanh toán"
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Payment created - PaymentId: {PaymentId}, Amount: {Amount}, Status: {Status}",
                    payment.PaymentId, payment.Amount, payment.PaymentStatus);

                // 8. Cập nhật Voucher usage
                if (booking.VoucherId.HasValue && userId != null)
                {
                    _logger.LogInformation("Updating voucher usage for VoucherId: {VoucherId}", booking.VoucherId);

                    var userVoucher = await _context.UserVouchers
                        .FirstOrDefaultAsync(uv =>
                            uv.UserId == userId &&
                            uv.VoucherId == booking.VoucherId &&
                            !uv.IsUsed);

                    if (userVoucher != null)
                    {
                        userVoucher.IsUsed = true;
                        userVoucher.ClaimDate = DateTime.Now;
                        _context.UserVouchers.Update(userVoucher);
                        _logger.LogInformation("Marked UserVoucher as used");
                    }
                    else
                    {
                        _logger.LogWarning("UserVoucher not found for UserId={UserId}, VoucherId={VoucherId}",
                            userId, booking.VoucherId);
                    }

                    var voucher = await _context.Vouchers.FindAsync(booking.VoucherId);
                    if (voucher != null)
                    {
                        voucher.UsedCount++;
                        _context.Vouchers.Update(voucher);
                        _logger.LogInformation("Incremented voucher UsedCount to {UsedCount}/{UsageLimit}",
                            voucher.UsedCount, voucher.UsageLimit);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ Voucher usage updated successfully");
                }

                // Commit transaction
                await transaction.CommitAsync();
                _logger.LogInformation("✓✓✓ TRANSACTION COMMITTED SUCCESSFULLY ✓✓✓");
                _logger.LogInformation("Summary: BookingId={BookingId}, PaymentId={PaymentId}, Amount={Amount}, Seats={SeatCount}, Foods={FoodCount}",
                    booking.BookingId, payment.PaymentId, payment.Amount,
                    tempBooking.SelectedSeatIds.Count, tempBooking.SelectedFoods.Count);

                // Xóa Session/TempData
                HttpContext.Session.Remove("TempBooking");
                TempData.Remove("TempBookingJson");
                _logger.LogInformation("Cleaned up session and TempData");

                // Prepare model for View
                var viewModel = new PaymentResponseModel
                {
                    Success = true,
                    TransactionId = response.TransactionId,
                    OrderId = response.OrderId,
                    PaymentMethod = "VNPay",
                    OrderDescription = user?.Email ?? "Unknown",
                    VnPayResponseCode = response.VnPayResponseCode,
                    Token = response.Token,
                    Amount = booking.FinalAmount ?? booking.TotalPrice ?? 0m
                };

                ViewBag.BookingId = booking.BookingId;
                ViewBag.PaymentId = payment.PaymentId;

                _logger.LogInformation("=== PaymentCallbackVnpay SUCCESS - BookingId: {BookingId}, Amount: {Amount} ===",
                    booking.BookingId, viewModel.Amount);

                return View(viewModel);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();

                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                var stackTrace = dbEx.StackTrace ?? "No stack trace";

                _logger.LogError(dbEx, "DATABASE ERROR: {Message}\nInner: {Inner}\nStack: {Stack}",
                    dbEx.Message, innerMsg, stackTrace);

                TempData["ErrorMessage"] = $"Lỗi cơ sở dữ liệu: {innerMsg}";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                var stackTrace = ex.StackTrace ?? "No stack trace";

                _logger.LogError(ex, "GENERAL ERROR: {Message}\nInner: {Inner}\nStack: {Stack}",
                    ex.Message, innerMsg, stackTrace);

                TempData["ErrorMessage"] = $"Lỗi khi lưu dữ liệu: {innerMsg}";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [Route("CreatePaymentUrl")]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model)
        {
            var response = await _momoService.CreatePaymentMomo(model);
            return Redirect(response.PayUrl);
        }

        public async Task<IActionResult> Index()
        {
            var dBContext = _context.Payments.Include(p => p.Booking);
            return View(await dBContext.ToListAsync());
        }

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
                .Include(p => p.Booking)
                .ThenInclude(b => b.BookingFoods)
                .ThenInclude(bf => bf.FoodItem)
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
            var userId = _userManager.GetUserId(User);

            var bookings = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Showtime).ThenInclude(st => st.Movie)
                .Include(b => b.Showtime).ThenInclude(st => st.Room)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Include(b => b.BookingFoods).ThenInclude(bf => bf.FoodItem)
                .Where(b => b.UserId == userId)
                .ToList();

            // ✅ FIX: Use PaymentStatus constant
            var payments = _context.Payments
                .Where(p => p.PaymentStatus == PaymentStatus.Completed)
                .ToList();

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

            if (newAmount < 5000 && newAmount > 0)
            {
                return Json(new { success = false, message = "Sau giảm giá, số tiền phải ít nhất 5.000 VNĐ để thanh toán!" });
            }

            tempBooking.VoucherId = voucher.VoucherId;
            tempBooking.VoucherUsed = voucher.Code;
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