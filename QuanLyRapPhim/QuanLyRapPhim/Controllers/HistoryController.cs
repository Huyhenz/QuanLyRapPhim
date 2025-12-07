using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    [Authorize]
    public class HistoryController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;

        public HistoryController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> TicketHistory()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            // ✅ FIX: Use PaymentStatus.Completed constant
            var bookings = await _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Include(b => b.BookingFoods).ThenInclude(bf => bf.FoodItem)
                .Include(b => b.Payment)
                .Where(b => b.UserId == userId &&
                           b.Payment != null &&
                           b.Payment.PaymentStatus == PaymentStatus.Completed) // ✅ Use constant "Completed"
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(bookings);
        }
    }
}