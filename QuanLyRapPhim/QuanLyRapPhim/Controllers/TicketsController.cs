using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System.Threading.Tasks;

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
            {
                return NotFound();
            }

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            foreach (var seat in showtime.Room.Seats)
            {
                seat.Status = bookedSeats.Contains(seat.SeatId) ? "Taken" : "Available";
            }

            return View(showtime);
        }

        // POST: Tickets/ConfirmBooking
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
            {
                return NotFound();
            }

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            foreach (var seatId in selectedSeats)
            {
                if (bookedSeats.Contains(seatId))
                {
                    TempData["ErrorMessage"] = "Một hoặc nhiều ghế đã được đặt. Vui lòng chọn ghế khác.";
                    return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
                }
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

            booking.TotalPrice = booking.BookingDetails.Sum(bd => bd.Price);

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Chuyển hướng sang trang thanh toán
            return RedirectToAction("Create", "Payments", new { bookingId = booking.BookingId });
        }
    }
}