using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
            ViewBag.Movies = _context.Movies.ToList(); // Thay bằng logic lấy dữ liệu thực tế
            ViewBag.Showtimes = _context.Showtimes.Include(s => s.Movie).Include(s => s.Room).ToList(); // Thay bằng logic lấy dữ liệu thực tế
            ViewBag.Rooms = _context.Rooms.ToList(); // Để populate dropdown trong modal
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

        public IActionResult RevenueStatistics()
        {
            // Lấy tất cả bookings cùng thông tin liên quan
            var bookings = _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .ToList();

            // Tính tổng doanh thu
            var totalRevenue = bookings.Sum(b => b.TotalPrice);

            // Tính doanh thu theo phim
            var revenueByMovie = bookings
                .GroupBy(b => b.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    Revenue = g.Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.Revenue) // Sắp xếp theo doanh thu giảm dần
                .ToList();

            // Tính doanh thu theo ngày
            var revenueByDate = bookings
                .GroupBy(b => b.BookingDate.Date) // Nhóm theo ngày (bỏ qua thời gian)
                .Select(g => new RevenueByDateItem
                {
                    Date = g.Key,
                    Revenue = g.Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.Date) // Sắp xếp theo ngày tăng dần
                .ToList();

            // Tạo đối tượng dữ liệu để truyền sang view
            var revenueData = new
            {
                TotalRevenue = totalRevenue,
                RevenueByMovie = revenueByMovie,
                RevenueByDate = revenueByDate
            };

            // Chuyển đổi RevenueByDate thành định dạng phù hợp cho JavaScript
            var formattedRevenueByDate = revenueData.RevenueByDate.Select(x => new
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                Revenue = x.Revenue
            }).ToList();

            // Truyền dữ liệu sang view
            ViewBag.RevenueData = revenueData;
            ViewBag.FormattedRevenueByDate = JsonConvert.SerializeObject(formattedRevenueByDate);

            return View();
        }

        public IActionResult GetShowtime(int id)
        {
            // Cast ViewBag.Showtimes to IEnumerable<dynamic> to use LINQ
            var showtimes = (ViewBag.Showtimes as System.Collections.IEnumerable)?.Cast<dynamic>();
            if (showtimes == null)
            {
                return NotFound(new { success = false, message = "Showtime data is not available." });
            }

            // Use FirstOrDefault with the casted collection
            var showtime = showtimes.FirstOrDefault(s => (int)s.ShowtimeId == id);
            if (showtime == null)
            {
                return NotFound(new { success = false, message = "Showtime not found." });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    showtime.ShowtimeId,
                    showtime.MovieId,
                    showtime.RoomId,
                    Date = showtime.Date.ToString("yyyy-MM-ddTHH:mm"), // Format for datetime-local input
                    showtime.TicketPrice
                }
            });
        }
    }
}