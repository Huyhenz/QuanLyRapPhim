using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System.Security.Claims;

namespace QuanLyRapPhim.Controllers
{
    public class HistoryController : Controller
    {
        private readonly DBContext _context;

        public HistoryController(DBContext context)
        {
            _context = context;
        }

        public IActionResult TicketHistory()
        {
            // Lấy ID của người dùng hiện tại
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Lấy danh sách các booking của người dùng hiện tại và đã thanh toán (PaymentStatus = "Completed")
            var bookings = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails).ThenInclude(d => d.Seat)
                .Where(b => b.UserId == currentUserId)
                .ToList();

            // Lấy danh sách payment có trạng thái "Completed" và thuộc về các booking của người dùng
            var payments = _context.Payments
                .Where(p => p.PaymentStatus == "Completed" && bookings.Select(b => b.BookingId).Contains(p.BookingId))
                .ToList();

            // Lọc lại bookings chỉ giữ những booking có payment "Completed"
            bookings = bookings
                .Where(b => payments.Any(p => p.BookingId == b.BookingId))
                .ToList();

            // Gửi dữ liệu qua ViewBag
            ViewBag.Payments = payments;
            ViewBag.CurrentUserId = currentUserId;

            return View(bookings);
        }
    }
}