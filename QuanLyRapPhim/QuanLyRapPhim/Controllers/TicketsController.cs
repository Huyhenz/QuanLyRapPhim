using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace QuanLyRapPhim.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;

        public TicketsController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tickets/SelectRoomAndSeat
        public async Task<IActionResult> SelectRoomAndSeat(int showtimeId)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Room)
                .ThenInclude(r => r.Seats)
                .Include(s => s.Movie)
                .AsNoTracking() // Thêm AsNoTracking
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null) return NotFound();

            // Load TempBooking từ Session
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null || tempBooking.ShowtimeId != showtimeId)
            {
                tempBooking = new TempBooking { ShowtimeId = showtimeId };
                SaveTempBookingToSession(tempBooking);
            }

            // Truyền selected seats từ temp để hiển thị (nếu quay lại)
            ViewBag.SelectedSeats = tempBooking.SelectedSeatIds;
            ViewBag.Showtime = showtime;

            return View(showtime);
        }

 
        // POST: Tickets/SelectRoomAndSeat
        [HttpPost]
        public async Task<IActionResult> SelectRoomAndSeat(int showtimeId, List<int> selectedSeatIds)
        {
            if (selectedSeatIds == null || !selectedSeatIds.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế.";
                return RedirectToAction(nameof(SelectRoomAndSeat), new { showtimeId });
            }

            // Kiểm tra ghế có sẵn (không lưu DB, chỉ check)
            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            if (selectedSeatIds.Any(s => bookedSeats.Contains(s)))
            {
                TempData["ErrorMessage"] = "Một hoặc nhiều ghế đã được đặt. Vui lòng chọn ghế khác.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            // Load showtime để tính giá
            var showtime = await _context.Showtimes
                .Include(s => s.Room)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction("Index", "Showtimes");
            }

            if (showtime.Room == null)
            {
                TempData["ErrorMessage"] = "Thông tin phòng chiếu không hợp lệ.";
                return RedirectToAction("Index", "Showtimes");
            }

            // Load hoặc tạo TempBooking
            var tempBooking = GetTempBookingFromSession() ?? new TempBooking();
            tempBooking.ShowtimeId = showtimeId;
            tempBooking.SelectedSeatIds = selectedSeatIds;

            // Tính số hàng để xác định giá
            var totalSeatsInRoom = await _context.Seats.CountAsync(s => s.RoomId == showtime.RoomId);
            var totalRows = (int)Math.Ceiling((double)totalSeatsInRoom / 10);
            char lastRowLabel = (char)('A' + totalRows - 1);
            const decimal normalPrice = 50000;
            const decimal couplePrice = 100000;

            // Load seat info riêng để tính giá
            var seatInfos = await _context.Seats
                .Where(s => selectedSeatIds.Contains(s.SeatId))
                .Select(s => new { s.SeatId, s.SeatNumber })
                .AsNoTracking()
                .ToListAsync();

            decimal totalSeatPrice = 0;
            foreach (var seatInfo in seatInfos)
            {
                totalSeatPrice += seatInfo.SeatNumber.StartsWith(lastRowLabel.ToString())
                    ? couplePrice
                    : normalPrice;
            }

            // Cộng thêm giá đồ ăn nếu đã chọn trước đó
            decimal existingFoodPrice = tempBooking.SelectedFoods?.Sum(f => f.Quantity * f.UnitPrice) ?? 0;
            tempBooking.TotalPrice = totalSeatPrice + existingFoodPrice;
            tempBooking.FinalAmount = tempBooking.TotalPrice;

            // Lưu vào Session
            SaveTempBookingToSession(tempBooking);

            TempData["SuccessMessage"] = "Đã chọn ghế thành công!";
            return RedirectToAction("SelectFood");
        }

        // GET: Tickets/SelectFood
        // GET: Tickets/SelectFood
        public async Task<IActionResult> SelectFood()
        {
            // Load TempBooking từ Session
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn lịch chiếu và ghế trước.";
                return RedirectToAction("Index", "Showtimes");
            }

            var foodItems = await _context.FoodItems.ToListAsync();
            ViewBag.FoodItems = foodItems;
            ViewBag.SelectedFoods = tempBooking.SelectedFoods;

            // Truyền thêm info showtime nếu có
            if (tempBooking.ShowtimeId.HasValue)
            {
                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room) // QUAN TRỌNG: Phải Include Room
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId);

                // Kiểm tra showtime có tồn tại không
                if (showtime == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy suất chiếu. Vui lòng chọn lại.";
                    HttpContext.Session.Remove("TempBooking");
                    return RedirectToAction("Index", "Showtimes");
                }

                ViewBag.Showtime = showtime;
            }
            else
            {
                // Trường hợp QuickFoodBooking (mua đồ ăn không cần vé)
                ViewBag.Showtime = null;
            }

            return View();
        }

        // POST: Tickets/SelectFood - LƯU VÀO SESSION
        // POST: Tickets/SelectFood
        [HttpPost]
        public async Task<IActionResult> SelectFood(Dictionary<int, int> selectedFoods)
        {
            // Load TempBooking
            var tempBooking = GetTempBookingFromSession();
            if (tempBooking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy dữ liệu tạm.";
                return RedirectToAction("Index", "Showtimes");
            }

            // Cập nhật danh sách thức ăn
            tempBooking.SelectedFoods.Clear();
            foreach (var item in selectedFoods.Where(s => s.Value > 0))
            {
                var food = await _context.FoodItems.FindAsync(item.Key);
                if (food != null)
                {
                    tempBooking.SelectedFoods.Add(new TempFoodItem
                    {
                        FoodItemId = item.Key,
                        Quantity = item.Value,
                        UnitPrice = food.Price
                    });
                }
            }

            // Tính lại tổng giá (ghế + đồ ăn)
            decimal totalFoodPrice = tempBooking.SelectedFoods.Sum(f => f.Quantity * f.UnitPrice);
            decimal totalSeatPrice = 0;

            // Tính giá ghế nếu có showtime
            if (tempBooking.ShowtimeId.HasValue && tempBooking.SelectedSeatIds.Any())
            {
                var showtime = await _context.Showtimes
                    .Include(s => s.Room)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShowtimeId == tempBooking.ShowtimeId.Value);

                if (showtime != null && showtime.Room != null)
                {
                    var totalSeatsInRoom = await _context.Seats.CountAsync(s => s.RoomId == showtime.RoomId);
                    var totalRows = (int)Math.Ceiling((double)totalSeatsInRoom / 10);
                    char lastRowLabel = (char)('A' + totalRows - 1);
                    const decimal normalPrice = 50000;
                    const decimal couplePrice = 100000;

                    var seatInfos = await _context.Seats
                        .Where(s => tempBooking.SelectedSeatIds.Contains(s.SeatId))
                        .Select(s => new { s.SeatId, s.SeatNumber })
                        .AsNoTracking()
                        .ToListAsync();

                    foreach (var seatInfo in seatInfos)
                    {
                        totalSeatPrice += seatInfo.SeatNumber.StartsWith(lastRowLabel.ToString())
                            ? couplePrice
                            : normalPrice;
                    }
                }
                else
                {
                    // Showtime không tồn tại hoặc Room null
                    TempData["ErrorMessage"] = "Suất chiếu không hợp lệ. Vui lòng chọn lại.";
                    HttpContext.Session.Remove("TempBooking");
                    return RedirectToAction("Index", "Showtimes");
                }
            }

            tempBooking.TotalPrice = totalSeatPrice + totalFoodPrice;
            tempBooking.FinalAmount = tempBooking.TotalPrice;

            SaveTempBookingToSession(tempBooking);

            TempData["SuccessMessage"] = "Đã chọn thức ăn thành công!";
            return RedirectToAction("Create", "Payments");
        }

        // GET: Tickets/QuickFoodBooking (Mua đồ ăn không cần xem phim)
        public IActionResult QuickFoodBooking()
        {
            var tempBooking = new TempBooking { ShowtimeId = null };
            SaveTempBookingToSession(tempBooking);
            return RedirectToAction("SelectFood");
        }

        // POST: Tickets/CancelBooking (Hủy toàn bộ và xóa Session)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelBooking()
        {
            HttpContext.Session.Remove("TempBooking");
            TempData["SuccessMessage"] = "Đã hủy thành công! Bạn có thể chọn lại ghế và đồ ăn.";
            return RedirectToAction("Index", "Home");
        }

        // Helper methods
        private TempBooking GetTempBookingFromSession()
        {
            var tempBookingJson = HttpContext.Session.GetString("TempBooking");
            return string.IsNullOrEmpty(tempBookingJson)
                ? null
                : JsonSerializer.Deserialize<TempBooking>(tempBookingJson);
        }

        private void SaveTempBookingToSession(TempBooking tempBooking)
        {
            HttpContext.Session.SetString("TempBooking", JsonSerializer.Serialize(tempBooking));
        }

        // ============================================================================
        // CÁC METHODS DƯỚI ĐÂY KHÔNG DÙNG - XÓA HOẶC COMMENT
        // Vì bạn đang dùng Session-based flow, không lưu DB cho đến khi thanh toán
        // ============================================================================

        /*
        // XÓA - Method này lưu DB ngay, không theo Session flow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBooking(int showtimeId, List<int> selectedSeats)
        {
            // ... code ...
        }

        // XÓA - Method này lưu DB ngay, không cần thiết
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFood(int bookingId, int showtimeId, Dictionary<int, int> quantities)
        {
            // ... code ...
        }

        // XÓA - Method này xử lý BookingFoods đã lưu DB, không cần vì chưa lưu DB
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelFood(int bookingId)
        {
            // ... code ...
        }
        */
    }
}