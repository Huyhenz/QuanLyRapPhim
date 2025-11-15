using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System;
using System.IO;
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
        public IActionResult Movies()
        {
            var movies = _context.Movies.ToList();
            ViewBag.Movies = movies; // Pass movies to the view via ViewBag
            return View();
        }
        // Other actions like CreateMovie, EditMovie, DeleteMovie, etc.
        [HttpPost]
        public IActionResult CreateMovie(Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Movies.Add(movie);
                _context.SaveChanges();
                return Json(new { success = true, message = "Movie added successfully!" });
            }
            return Json(new { success = false, message = "Error adding movie.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }
        [HttpPost]
        public IActionResult EditMovie(Movie movie)
        {
            if (ModelState.IsValid)
            {
                var existingMovie = _context.Movies.Find(movie.MovieId);
                if (existingMovie == null)
                    return Json(new { success = false, message = "Movie not found." });
                existingMovie.Title = movie.Title;
                existingMovie.Genre = movie.Genre;
                existingMovie.Duration = movie.Duration;
                existingMovie.Director = movie.Director;
                existingMovie.Poster = movie.Poster;
                existingMovie.Description = movie.Description;
                existingMovie.Actors = movie.Actors;
                existingMovie.TrailerUrl = movie.TrailerUrl;
                _context.SaveChanges();
                return Json(new { success = true, message = "Movie updated successfully!" });
            }
            return Json(new { success = false, message = "Error updating movie.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }
        [HttpPost]
        public IActionResult DeleteMovie(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null)
                return Json(new { success = false, message = "Movie not found." });
            _context.Movies.Remove(movie);
            _context.SaveChanges();
            return Json(new { success = true, message = "Movie deleted successfully!" });
        }
        [HttpGet]
        public IActionResult GetMovieDetails(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null)
            {
                return NotFound();
            }
            return Json(new
            {
                title = movie.Title,
                description = movie.Description,
                duration = movie.Duration,
                poster = movie.Poster,
                genre = movie.Genre,
                director = movie.Director,
                actors = movie.Actors,
                trailerUrl = movie.TrailerUrl
            });
        }
        public IActionResult ManageShowtimes()
        {
            ViewBag.Showtimes = _context.Showtimes.Include(s => s.Movie).Include(s => s.Room).ToList();
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View();
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
        // var showtime = _context.Showtimes.Find(ShowtimeId);
        // if (showtime == null) return Json(new { success = false, message = "Showtime not found." });
        // showtime.MovieId = MovieId;
        // showtime.Title = Title;
        // showtime.Poster = Poster;
        // showtime.RoomId = RoomId;
        // showtime.Date = Date;
        // showtime.StartTime = TimeSpan.Parse(StartTime); // Ensure StartTime is a TimeSpan in the model
        // _context.SaveChanges();
        // return Json(new { success = true, message = "Showtime updated successfully!" });
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

        // ========================================
        // QUẢN LÝ ĐỒ ĂN & NƯỚC UỐNG (FOOD ITEMS)
        // ========================================
        public IActionResult ManageFoods()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetFoodItems()
        {
            var foods = await _context.FoodItems
                .Select(f => new
                {
                    foodItemId = f.FoodItemId,
                    name = f.Name,
                    size = f.Size,
                    price = f.Price,
                    category = f.Category,
                    imageUrl = f.ImageUrl
                })
                .ToListAsync();

            return Json(foods);
        }

        [HttpGet]
        public async Task<IActionResult> GetFoodItem(int id)
        {
            var food = await _context.FoodItems.FindAsync(id);
            if (food == null) return NotFound();

            return Json(new
            {
                foodItemId = food.FoodItemId,
                name = food.Name,
                size = food.Size,
                price = food.Price,
                category = food.Category,
                imageUrl = food.ImageUrl
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateFood([FromForm] FoodItem food, IFormFile imageFile)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "food");
                Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                food.ImageUrl = "/images/food/" + fileName;
            }

            _context.FoodItems.Add(food);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Thêm món thành công!" });
        }
        [HttpPost]
        public async Task<IActionResult> EditFood(
            [FromForm] int foodItemId,
            [FromForm] string name,
            [FromForm] string size,
            [FromForm] decimal price,
            [FromForm] string category,
            [FromForm] IFormFile imageFile)
        {
            if (string.IsNullOrWhiteSpace(name) || price < 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var existing = await _context.FoodItems.FindAsync(foodItemId);
            if (existing == null)
                return Json(new { success = false, message = "Không tìm thấy món ăn." });

            existing.Name = name.Trim();
            existing.Size = size;
            existing.Price = price;
            existing.Category = category;

            if (imageFile != null && imageFile.Length > 0)
            {
                // Xóa ảnh cũ
                if (!string.IsNullOrEmpty(existing.ImageUrl))
                {
                    var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existing.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "food");
                Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                existing.ImageUrl = "/images/food/" + fileName;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi lưu dữ liệu: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFood(int id)
        {
            var food = await _context.FoodItems.FindAsync(id);
            if (food == null)
                return Json(new { success = false, message = "Không tìm thấy món ăn." });

            // Xóa ảnh
            if (!string.IsNullOrEmpty(food.ImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", food.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.FoodItems.Remove(food);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa thành công!" });
        }
    }

    // Helper class for revenue statistics
    public class RevenueByDateItem
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
    }
}