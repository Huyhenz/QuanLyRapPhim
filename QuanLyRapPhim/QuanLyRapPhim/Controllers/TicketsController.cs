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
using Microsoft.EntityFrameworkCore.Storage;

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
                .Include(s => s.Movie)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Seats)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
                return NotFound();

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            foreach (var seat in showtime.Room.Seats)
            {
                seat.Status = bookedSeats.Contains(seat.SeatId) ? "Đã đặt" : "Trống";
            }

            return View(showtime);
        }

        // POST: Tickets/ConfirmBooking → Tạo Booking tạm + khóa ghế → Chuyển sang chọn bắp nước
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBooking(int showtimeId, List<int> selectedSeats)
        {
            if (selectedSeats == null || !selectedSeats.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Seats)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
                return NotFound();

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            if (selectedSeats.Any(s => bookedSeats.Contains(s)))
            {
                TempData["ErrorMessage"] = "Một hoặc nhiều ghế đã được đặt. Vui lòng chọn ghế khác.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            var user = await _userManager.GetUserAsync(User);
            var totalRows = (int)Math.Ceiling((double)showtime.Room.Seats.Count / 10);
            char lastRowLabel = (char)('A' + totalRows - 1);
            const decimal normalPrice = 50000;
            const decimal couplePrice = 100000;

            var booking = new Booking
            {
                ShowtimeId = showtimeId,
                BookingDate = DateTime.Now,
                UserId = user?.Id,
                TotalPrice = 0, // Sẽ cập nhật sau khi chọn bắp nước
                BookingDetails = new List<BookingDetail>()
            };

            foreach (var seatId in selectedSeats)
            {
                var seat = showtime.Room.Seats.FirstOrDefault(s => s.SeatId == seatId);
                if (seat != null)
                {
                    decimal price = seat.SeatNumber.StartsWith(lastRowLabel.ToString()) ? couplePrice : normalPrice;
                    booking.BookingDetails.Add(new BookingDetail
                    {
                        SeatId = seatId,
                        Price = price
                    });
                }
            }

            // Tạm tính tiền vé
            booking.TotalPrice = booking.BookingDetails.Sum(bd => bd.Price);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Cập nhật trạng thái ghế
                foreach (var seatId in selectedSeats)
                {
                    var seat = await _context.Seats.FindAsync(seatId);
                    if (seat != null)
                        seat.Status = "Đã đặt";
                }
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi đặt ghế. Vui lòng thử lại.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            // Lưu thông tin tạm để hiển thị ở trang bắp nước
            TempData["BookingId"] = booking.BookingId;
            TempData["SelectedSeats"] = string.Join(", ", selectedSeats.Select(id => showtime.Room.Seats.First(s => s.SeatId == id).SeatNumber));
            TempData["SeatCount"] = selectedSeats.Count;

            // Chuyển sang chọn bắp nước
            return RedirectToAction("SelectFood", new { bookingId = booking.BookingId });
        }

        // GET: Tickets/SelectFood
        public async Task<IActionResult> SelectFood(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Include(b => b.BookingFoods).ThenInclude(bf => bf.FoodItem)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return NotFound();

            ViewBag.FoodItems = await _context.FoodItems.ToListAsync();

            // Lấy số lượng hiện tại (nếu có)
            ViewBag.CurrentQuantities = booking.BookingFoods?
                .ToDictionary(bf => bf.FoodItemId, bf => bf.Quantity) ?? new Dictionary<int, int>();

            return View(booking.Showtime);
        }

        // POST: Tickets/ConfirmFood
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFood(int bookingId, int showtimeId, Dictionary<int, int> quantities)
        {
            if (quantities == null)
                quantities = new Dictionary<int, int>();

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Include(b => b.BookingFoods)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Xóa bắp nước cũ
                if (booking.BookingFoods != null && booking.BookingFoods.Any())
                    _context.BookingFoods.RemoveRange(booking.BookingFoods);

                decimal foodTotal = 0;
                foreach (var kvp in quantities.Where(q => q.Value > 0))
                {
                    var food = await _context.FoodItems.FindAsync(kvp.Key);
                    if (food != null)
                    {
                        var bookingFood = new BookingFood
                        {
                            BookingId = bookingId,
                            FoodItemId = food.FoodItemId,
                            Quantity = kvp.Value,
                            UnitPrice = food.Price
                        };
                        _context.BookingFoods.Add(bookingFood);
                        foodTotal += food.Price * kvp.Value;
                    }
                }

                // Cập nhật tổng tiền: vé + bắp nước
                booking.TotalPrice = booking.BookingDetails.Sum(bd => bd.Price) + foodTotal;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Tạo Payment
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.TotalPrice,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VNPay",
                    PaymentStatus = "Completed" // Sẽ cập nhật khi thanh toán thành công
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                return RedirectToAction("Create", "Payments", new { bookingId = booking.BookingId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi khi lưu đồ ăn: " + ex.Message;
                return RedirectToAction("SelectFood", new { bookingId });
            }
        }

        // POST: Tickets/CancelBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId, int showtimeId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Include(b => b.BookingFoods)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đặt vé.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Xóa bắp nước
                if (booking.BookingFoods != null && booking.BookingFoods.Any())
                    _context.BookingFoods.RemoveRange(booking.BookingFoods);

                // Xóa chi tiết ghế + mở ghế
                if (booking.BookingDetails != null)
                {
                    var seatIds = booking.BookingDetails.Select(bd => bd.SeatId).ToList();
                    _context.BookingDetails.RemoveRange(booking.BookingDetails);

                    foreach (var seatId in seatIds)
                    {
                        var seat = await _context.Seats.FindAsync(seatId);
                        if (seat != null)
                            seat.Status = "Trống";
                    }
                }

                // Xóa booking
                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Đã hủy đặt vé và bắp nước thành công.";
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi khi hủy đặt vé.";
            }

            return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
        }
    }
}