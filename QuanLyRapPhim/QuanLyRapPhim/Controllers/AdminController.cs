using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System.Threading.Tasks;

namespace QuanLyRapPhim.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;

        public AdminController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/ManageBookings
        public async Task<IActionResult> ManageBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
                .Include(b => b.User)
                .Where(b => b.UserId != null) // Lọc bỏ khách vãng lai
                .ToListAsync();

            var payments = await _context.Payments.ToListAsync();
            ViewBag.Payments = payments;

            return View(bookings);
        }

    }
}