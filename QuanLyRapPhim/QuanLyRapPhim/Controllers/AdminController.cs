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
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
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

        // GET: Get Movie Details
        [HttpGet]
        public IActionResult GetMovieDetails(int id)
        {
            try
            {
                var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Movie not found." });
                }
                return Json(new
                {
                    success = true,
                    message = "Movie details fetched successfully.",
                    data = new
                    {
                        MovieId = movie.MovieId,
                        Title = movie.Title,
                        Description = movie.Description,
                        Duration = movie.Duration,
                        Poster = movie.Poster,
                        Genre = movie.Genre,
                        Director = movie.Director,
                        Actors = movie.Actors,
                        TrailerUrl = movie.TrailerUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: Edit Movie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditMovie(Movie model)
        {
            try
            {
                var movie = _context.Movies.FirstOrDefault(m => m.MovieId == model.MovieId);
                if (movie == null)
                {
                    return Json(new { success = false, message = "Movie not found." });
                }
                movie.Title = model.Title;
                movie.Description = model.Description;
                movie.Duration = model.Duration;
                movie.Poster = model.Poster;
                movie.Genre = model.Genre;
                movie.Director = model.Director;
                movie.Actors = model.Actors;
                movie.TrailerUrl = model.TrailerUrl;
                _context.SaveChanges();
                return Json(new { success = true, message = "Movie updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
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
            try
            {
                if (_context.Showtimes.Any(s => s.MovieId == id))
                {
                    return Json(new { success = false, message = "Cannot delete movie with existing showtimes." });
                }
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Movie deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to delete movie: {ex.Message}" });
            }
        }

        // POST: Create Showtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShowtime([Bind("MovieId,RoomId,Title,Poster,StartTime,Date")] Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        return Json(new { success = false, message = "Invalid movie selected." });
                    }
                    if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        return Json(new { success = false, message = "Invalid room selected." });
                    }
                    _context.Add(showtime);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Showtime added successfully!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Failed to add showtime: {ex.Message}" });
                }
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Failed to add showtime.", errors });
        }

        // GET: Get Showtime Details
        [HttpGet]
        public IActionResult GetShowtimeDetails(int id)
        {
            try
            {
                var showtime = _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefault(s => s.ShowtimeId == id);
                if (showtime == null)
                {
                    return Json(new { success = false, message = "Showtime not found." });
                }
                return Json(new
                {
                    success = true,
                    message = "Showtime details fetched successfully.",
                    data = new
                    {
                        ShowtimeId = showtime.ShowtimeId,
                        MovieId = showtime.MovieId,
                        RoomId = showtime.RoomId,
                        Title = showtime.Title,
                        Poster = showtime.Poster,
                        StartTime = showtime.StartTime.ToString(@"hh\:mm\:ss"),
                        Date = showtime.Date.ToString("yyyy-MM-dd")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
        [HttpPost]
        //public IActionResult EditShowtime(int ShowtimeId, int MovieId, string Title, string Poster, int RoomId, DateTime Date, string StartTime)
        //{
        //    var showtime = _context.Showtimes.Find(ShowtimeId);
        //    if (showtime == null) return Json(new { success = false, message = "Showtime not found." });

        //    showtime.MovieId = MovieId;
        //    showtime.Title = Title;
        //    showtime.Poster = Poster;
        //    showtime.RoomId = RoomId;
        //    showtime.Date = Date;
        //    showtime.StartTime = TimeSpan.Parse(StartTime); // Ensure StartTime is a TimeSpan in the model

        //    _context.SaveChanges();
        //    return Json(new { success = true, message = "Showtime updated successfully!" });
        //}
        // POST: Edit Showtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShowtime([Bind("ShowtimeId,MovieId,RoomId,Title,Poster,StartTime,Date")] Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingShowtime = await _context.Showtimes.FindAsync(showtime.ShowtimeId);
                    if (existingShowtime == null)
                    {
                        return Json(new { success = false, message = "Showtime not found." });
                    }
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        return Json(new { success = false, message = "Invalid movie selected." });
                    }
                    if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        return Json(new { success = false, message = "Invalid room selected." });
                    }

                    existingShowtime.MovieId = showtime.MovieId;
                    existingShowtime.RoomId = showtime.RoomId;
                    existingShowtime.Title = showtime.Title;
                    existingShowtime.Poster = showtime.Poster;
                    existingShowtime.StartTime = showtime.StartTime;
                    existingShowtime.Date = showtime.Date;

                    _context.Update(existingShowtime);
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
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Failed to update showtime: {ex.Message}" });
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
            try
            {
                if (_context.Bookings.Any(b => b.ShowtimeId == id))
                {
                    return Json(new { success = false, message = "Cannot delete showtime with existing bookings." });
                }
                _context.Showtimes.Remove(showtime);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Showtime deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to delete showtime: {ex.Message}" });
            }
        }

        // GET: Get Payment Statuses
        [HttpGet]
        public async Task<IActionResult> GetPaymentStatuses()
        {
            var payments = await _context.Payments
                .Select(p => new { p.BookingId, p.PaymentStatus })
                .ToListAsync();
            return Json(payments);
        }

        // POST: Update Payment Status
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

        // GET: Manage Bookings
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

        // GET: Revenue Statistics
        public IActionResult RevenueStatistics()
        {
            var bookings = _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
                .ToList();

            var totalRevenue = bookings.Sum(b => b.TotalPrice);

            var revenueByMovie = bookings
                .GroupBy(b => b.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    Revenue = g.Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            var revenueByDate = bookings
                .GroupBy(b => b.BookingDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.Date)
                .ToList();

            var revenueData = new
            {
                TotalRevenue = totalRevenue,
                RevenueByMovie = revenueByMovie,
                RevenueByDate = revenueByDate
            };

            var formattedRevenueByDate = revenueData.RevenueByDate.Select(x => new
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                Revenue = x.Revenue
            }).ToList();

            ViewBag.RevenueData = revenueData;
            ViewBag.FormattedRevenueByDate = JsonConvert.SerializeObject(formattedRevenueByDate);

            return View();
        }
    }

    // Helper class for revenue statistics
    public class RevenueByDateItem
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
    }
}