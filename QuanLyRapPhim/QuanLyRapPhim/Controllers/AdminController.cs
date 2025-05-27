using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyRapPhim.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DBContext _context;

        public AdminController(DBContext context)
        {
            _context = context;
        }

        // GET: Admin Dashboard
        public IActionResult Index()
        {
            var movies = _context.Movies.ToList();
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .ToList();
            ViewBag.Movies = movies;
            ViewBag.Showtimes = showtimes;
            ViewBag.Rooms = _context.Rooms.ToList(); // For showtime creation
            return View();
        }

        // POST: Create Movie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMovie([Bind("Title,Description,Duration,Poster,Genre,Director,Actors,TrailerUrl")] Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Movie added successfully!" });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Failed to add movie.", errors });
        }

        // POST: Edit Movie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMovie(int id, [Bind("MovieId,Title,Description,Duration,Poster,Genre,Director,Actors,TrailerUrl")] Movie movie)
        {
            if (id != movie.MovieId)
            {
                return Json(new { success = false, message = "Invalid movie ID." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Movie updated successfully!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Movies.Any(e => e.MovieId == movie.MovieId))
                    {
                        return Json(new { success = false, message = "Movie not found." });
                    }
                    throw;
                }
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Failed to update movie.", errors });
        }

        // POST: Delete Movie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
            {
                return Json(new { success = false, message = "Movie not found." });
            }

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Movie deleted successfully!" });
        }

        // POST: Create Showtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShowtime([Bind("MovieId,RoomId,ShowDateTime,Price")] Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                _context.Add(showtime);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Showtime added successfully!" });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Failed to add showtime.", errors });
        }

        // POST: Edit Showtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShowtime(int id, [Bind("ShowtimeId,MovieId,RoomId,ShowDateTime,Price")] Showtime showtime)
        {
            if (id != showtime.ShowtimeId)
            {
                return Json(new { success = false, message = "Invalid showtime ID." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(showtime);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Showtime updated successfully!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Showtimes.Any(e => e.ShowtimeId == showtime.ShowtimeId))
                    {
                        return Json(new { success = false, message = "Showtime not found." });
                    }
                    throw;
                }
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Failed to update showtime.", errors });
        }

        // POST: Delete Showtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShowtime(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
            {
                return Json(new { success = false, message = "Showtime not found." });
            }

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Showtime deleted successfully!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetPaymentStatuses()
        {
            var payments = await _context.Payments
                .Select(p => new { p.BookingId, p.PaymentStatus })
                .ToListAsync();
            return Json(payments);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePaymentStatus(int bookingId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);
            if (payment != null)
            {
                return Json(new { success = true, paymentStatus = payment.PaymentStatus });
            }
            return Json(new { success = false, message = "No payment record found." });
        }

        public IActionResult ManageBookings()
        {
            var bookings = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s.Room)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Include(b => b.User)
                .ToList();
            ViewBag.Payments = _context.Payments.ToList();
            return View(bookings);
        }
    }
}