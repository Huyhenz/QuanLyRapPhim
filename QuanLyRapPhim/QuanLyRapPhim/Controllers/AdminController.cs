using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Showtimes = _context.Showtimes.Include(s => s.Movie).Include(s => s.Room).ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View();
        }

        public IActionResult Movies()
        {
            return View();
        }

        // ==================== QUẢN LÝ PHIM (MOVIES) ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMovie(Movie movie)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            try
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Thêm phim thành công!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Lỗi khi thêm phim: " + errorMsg });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMovie(Movie movie)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            if (movie.MovieId <= 0)
                return Json(new { success = false, message = "ID phim không hợp lệ." });

            try
            {
                var existingMovie = await _context.Movies.FindAsync(movie.MovieId);
                if (existingMovie == null)
                    return Json(new { success = false, message = "Không tìm thấy phim để sửa." });

                // Cập nhật các trường
                existingMovie.Title = movie.Title?.Trim();
                existingMovie.Description = movie.Description?.Trim();
                existingMovie.Duration = movie.Duration;
                existingMovie.Poster = movie.Poster?.Trim();
                existingMovie.Genre = movie.Genre?.Trim();
                existingMovie.Director = movie.Director?.Trim();
                existingMovie.Actors = movie.Actors?.Trim();
                existingMovie.TrailerUrl = movie.TrailerUrl?.Trim();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật phim thành công!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Lỗi khi sửa phim: " + errorMsg });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "ID phim không hợp lệ." });

            var movie = await _context.Movies
                .Include(m => m.Showtimes)
                .Include(m => m.Reviews)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return Json(new { success = false, message = "Không tìm thấy phim để xóa." });

            try
            {
                // Xóa tất cả Showtimes liên quan (cascade)
                if (movie.Showtimes != null && movie.Showtimes.Any())
                {
                    _context.Showtimes.RemoveRange(movie.Showtimes);
                }

                // Xóa đánh giá (Reviews) nếu có
                if (movie.Reviews != null && movie.Reviews.Any())
                {
                    _context.Reviews.RemoveRange(movie.Reviews);
                }

                // Xóa phim
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Đã xóa phim \"{movie.Title}\" và tất cả lịch chiếu liên quan thành công!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;

                if (errorMsg.Contains("REFERENCE constraint") || errorMsg.Contains("foreign key"))
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không thể xóa phim \"{movie.Title}\" vì đang được sử dụng ở đặt vé hoặc các bảng khác!"
                    });
                }

                return Json(new { success = false, message = "Lỗi khi xóa phim: " + errorMsg });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMovieDetails(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound();

            return Json(new
            {
                movieId = movie.MovieId,
                title = movie.Title ?? "",
                description = movie.Description ?? "",
                duration = movie.Duration,
                poster = movie.Poster ?? "",
                genre = movie.Genre ?? "",
                director = movie.Director ?? "",
                actors = movie.Actors ?? "",
                trailerUrl = movie.TrailerUrl ?? ""
            });
        }

        // ==================== QUẢN LÝ LỊCH CHIẾU (SHOWTIMES) ====================

        public IActionResult ManageShowtimes()
        {
            ViewBag.Showtimes = _context.Showtimes.Include(s => s.Movie).Include(s => s.Room).ToList();
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View();
        }

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

        // ==================== QUẢN LÝ THANH TOÁN VÀ ĐẶT VÉ ====================

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

        // ==================== QUẢN LÝ ĐẶT VÉ (BOOKINGS) ====================

        public async Task<IActionResult> ManageBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.User) // Để hiển thị User.FullName hoặc Email
                .Include(b => b.Showtime) // Để hiển thị Showtime.Date, StartTime
                    .ThenInclude(s => s.Movie) // Để hiển thị Movie.Title
                .Include(b => b.Showtime)
                    .ThenInclude(s => s.Room) // Để hiển thị Room.RoomName
                .Include(b => b.BookingDetails) // Để hiển thị BookingDetails (ghế)
                    .ThenInclude(bd => bd.Seat) // Để hiển thị Seat.SeatNumber
                .Include(b => b.BookingFoods) // Để hiển thị BookingFoods (đồ ăn/đồ uống) <--- Sửa chính ở đây
                    .ThenInclude(bf => bf.FoodItem) // Để hiển thị FoodItem.Name
                .OrderByDescending(b => b.BookingDate) // Tùy chọn: Sắp xếp theo ngày đặt mới nhất
                .ToListAsync();

            return View(bookings);
        }

        // ==================== THỐNG KÊ DOANH THU ====================

        public IActionResult RevenueStatistics(DateTime? fromDate, DateTime? toDate)
        {
            fromDate ??= DateTime.Now.AddMonths(-1).Date;
            toDate ??= DateTime.Now.Date;

            var validBookings = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.BookingDetails!).ThenInclude(bd => bd.Seat)
                .Include(b => b.BookingFoods!).ThenInclude(bf => bf.FoodItem)
                .Include(b => b.Payment)
                .Where(b => b.Payment != null
                            && b.Payment.PaymentStatus == "Completed"
                            && b.TotalPrice > 0
                            && b.BookingDate >= fromDate
                            && b.BookingDate <= toDate)
                .AsEnumerable()
                .Where(b => b.Showtime != null && b.Showtime.Movie != null)
                .ToList();

            var totalTicketRevenue = validBookings.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m);
            var totalFoodRevenue = validBookings.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Food").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m);
            var totalDrinkRevenue = validBookings.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Drink").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m);
            var totalComboRevenue = validBookings.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Combo").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m);
            var totalRevenue = validBookings.Sum(b => b.TotalPrice);

            var revenueByMovie = validBookings
                .GroupBy(b => b.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieTitle = g.Key ?? "Không xác định",
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    TotalRevenue = g.Sum(b => b.TotalPrice)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            var revenueByDate = validBookings
                .GroupBy(b => b.BookingDate.Date)
                .Select(g => new RevenueByDateItem
                {
                    Date = g.Key,
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    FoodRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Food").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    DrinkRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Drink").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    ComboRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Combo").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    TotalRevenue = g.Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.Date)
                .ToList();

            var revenueByMonth = validBookings
                .GroupBy(b => new { b.BookingDate.Year, b.BookingDate.Month })
                .Select(g => new RevenueByMonthItem
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    FoodRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Food").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    DrinkRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Drink").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    ComboRevenue = g.Sum(b => b.BookingFoods?.Where(bf => bf.FoodItem?.Category == "Combo").Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity) ?? 0m),
                    TotalRevenue = g.Sum(b => b.TotalPrice)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            var allBookingFoods = validBookings
                .SelectMany(b => b.BookingFoods ?? new List<BookingFood>());

            var revenueByFoodCategoryByDate = allBookingFoods
                .GroupBy(bf => new { Date = bf.Booking.BookingDate.Date, Category = bf.FoodItem?.Category ?? "Không xác định" })
                .Select(g => new FoodRevenueByDateItem
                {
                    Date = g.Key.Date,
                    Category = g.Key.Category,
                    Revenue = g.Sum(bf => (bf.FoodItem?.Price ?? 0m) * bf.Quantity)
                })
                .GroupBy(item => item.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Categories = g.ToList()
                })
                .OrderBy(x => x.Date)
                .ToList();

            var revenueData = new
            {
                FromDate = fromDate.Value.ToString("yyyy-MM-dd"),
                ToDate = toDate.Value.ToString("yyyy-MM-dd"),
                TotalTicketRevenue = totalTicketRevenue,
                TotalFoodRevenue = totalFoodRevenue,
                TotalDrinkRevenue = totalDrinkRevenue,
                TotalComboRevenue = totalComboRevenue,
                TotalRevenue = totalRevenue,
                RevenueByMovie = revenueByMovie,
                RevenueByDate = revenueByDate,
                RevenueByMonth = revenueByMonth,
                RevenueByFoodCategoryByDate = revenueByFoodCategoryByDate
            };

            var formattedRevenueByDateForChart = revenueByDate.Select(x => new
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                TicketRevenue = x.TicketRevenue,
                FoodRevenue = x.FoodRevenue,
                DrinkRevenue = x.DrinkRevenue,
                ComboRevenue = x.ComboRevenue,
                TotalRevenue = x.TotalRevenue
            }).ToList();

            var formattedRevenueByMonthForChart = revenueByMonth.Select(x => new
            {
                Month = $"{x.MonthName} {x.Year}",
                TicketRevenue = x.TicketRevenue,
                FoodRevenue = x.FoodRevenue,
                DrinkRevenue = x.DrinkRevenue,
                ComboRevenue = x.ComboRevenue,
                TotalRevenue = x.TotalRevenue
            }).ToList();

            ViewBag.RevenueData = revenueData;
            ViewBag.FormattedRevenueByDate = JsonConvert.SerializeObject(formattedRevenueByDateForChart);
            ViewBag.FormattedRevenueByMonth = JsonConvert.SerializeObject(formattedRevenueByMonthForChart);
            ViewBag.FormattedFoodByDate = JsonConvert.SerializeObject(revenueByFoodCategoryByDate);

            return View();
        }

        // ==================== QUẢN LÝ ĐỒ ĂN & NƯỚC UỐNG (FOOD ITEMS) ====================

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

    // Helper classes for revenue statistics
    public class RevenueByDateItem
    {
        public DateTime Date { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal FoodRevenue { get; set; }
        public decimal DrinkRevenue { get; set; }
        public decimal ComboRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class RevenueByMonthItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal FoodRevenue { get; set; }
        public decimal DrinkRevenue { get; set; }
        public decimal ComboRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class FoodRevenueByDateItem
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public decimal Revenue { get; set; }
    }
}