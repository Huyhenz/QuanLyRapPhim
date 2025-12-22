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
        private readonly ILogger<AdminController> _logger;

        public AdminController(DBContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
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
                return Json(new { success = false, message = "Invalid data.", errors });
            }

            try
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Movie added successfully!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error creating movie");
                return Json(new { success = false, message = "Error adding movie: " + errorMsg });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMovie(Movie movie)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Invalid data.", errors });
            }

            if (movie.MovieId <= 0)
                return Json(new { success = false, message = "Invalid movie ID." });

            try
            {
                var existingMovie = await _context.Movies.FindAsync(movie.MovieId);
                if (existingMovie == null)
                    return Json(new { success = false, message = "Movie not found." });

                existingMovie.Title = movie.Title?.Trim();
                existingMovie.Description = movie.Description?.Trim();
                existingMovie.Duration = movie.Duration;
                existingMovie.Poster = movie.Poster?.Trim();
                existingMovie.Genre = movie.Genre?.Trim();
                existingMovie.Director = movie.Director?.Trim();
                existingMovie.Actors = movie.Actors?.Trim();
                existingMovie.TrailerUrl = movie.TrailerUrl?.Trim();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Movie updated successfully!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error updating movie {MovieId}", movie.MovieId);
                return Json(new { success = false, message = "Error updating movie: " + errorMsg });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "Invalid movie ID." });

            var movie = await _context.Movies
                .Include(m => m.Showtimes)
                .Include(m => m.Reviews)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return Json(new { success = false, message = "Movie not found." });

            try
            {
                if (movie.Showtimes != null && movie.Showtimes.Any())
                {
                    _context.Showtimes.RemoveRange(movie.Showtimes);
                }

                if (movie.Reviews != null && movie.Reviews.Any())
                {
                    _context.Reviews.RemoveRange(movie.Reviews);
                }

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Movie \"{movie.Title}\" and all related showtimes deleted successfully!" });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error deleting movie {MovieId}", id);

                if (errorMsg.Contains("REFERENCE constraint") || errorMsg.Contains("foreign key"))
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete movie \"{movie.Title}\" because it is being used in bookings or other tables!"
                    });
                }

                return Json(new { success = false, message = "Error deleting movie: " + errorMsg });
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
                    _logger.LogError(ex, "Error creating showtime");
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
                _logger.LogError(ex, "Error getting showtime details {ShowtimeId}", id);
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
                    _logger.LogError(ex, "Error updating showtime {ShowtimeId}", showtime.ShowtimeId);
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
                _logger.LogError(ex, "Error deleting showtime {ShowtimeId}", id);
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
            try
            {
                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Showtime)
                        .ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime)
                        .ThenInclude(s => s.Room)
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Seat)
                    .Include(b => b.BookingFoods)
                        .ThenInclude(bf => bf.FoodItem)
                    .Include(b => b.Payment) // ✅ Load Payment to check status
                    .AsNoTracking() // Performance improvement
                    .OrderByDescending(b => b.BookingDate)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} bookings for management", bookings.Count);
                return View(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bookings");
                TempData["ErrorMessage"] = "Error loading bookings. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Admin/FailedBookings - Quản lý đặt vé thất bại
        public async Task<IActionResult> FailedBookings()
        {
            try
            {
                // Lấy danh sách thanh toán thất bại từ bảng PaymentFaileds (tất cả users cho admin)
                var failedPayments = await _context.PaymentFaileds
                    .OrderByDescending(pf => pf.FailedDate)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} failed bookings for management", failedPayments.Count);
                return View(failedPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading failed bookings");
                TempData["ErrorMessage"] = "Error loading failed bookings. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ==================== THỐNG KÊ DOANH THU (CẢI TIẾN) ====================

        // ==================== THỐNG KÊ DOANH THU ====================
        // (Thay thế method RevenueStatistics trong AdminController.cs)

        // ==================== THỐNG KÊ DOANH THU (ĐÃ SỬA) ====================
        // ==================== THỐNG KÊ DOANH THU (ĐÃ SỬA) ====================
        public IActionResult RevenueStatistics(DateTime? fromDate, DateTime? toDate)
        {
            fromDate ??= DateTime.Now.AddMonths(-3).Date;
            toDate ??= DateTime.Now.Date.AddDays(1).AddSeconds(-1);

            _logger.LogInformation("=== REVENUE STATISTICS DEBUG ===");
            _logger.LogInformation("Date Range: {FromDate} to {ToDate}", fromDate, toDate);

            // ✅ Helper function để normalize category
            string NormalizeCategory(string category)
            {
                if (string.IsNullOrWhiteSpace(category)) return "Unknown";

                var lower = category.ToLower().Trim();

                // Food variations
                if (lower == "food" || lower == "bắp" || lower == "thức ăn" ||
                    lower == "popcorn" || lower == "bap")
                    return "Food";

                // Drink variations  
                if (lower == "drink" || lower == "nước" || lower == "nuoc" ||
                    lower == "thức uống" || lower == "thuc uong" || lower == "nước uống")
                    return "Drink";

                // Combo
                if (lower == "combo")
                    return "Combo";

                return category; // Return original if no match
            }

            // Lấy tất cả bookings
            var bookingsInRange = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Seat)
                .Include(b => b.BookingFoods).ThenInclude(bf => bf.FoodItem)
                .Include(b => b.Payment)
                .Where(b => b.BookingDate >= fromDate && b.BookingDate <= toDate)
                .ToList();

            _logger.LogInformation("Total Bookings: {Count}", bookingsInRange.Count);

            // ✅ Kiểm tra chi tiết BookingFoods
            var totalBookingFoods = bookingsInRange.SelectMany(b => b.BookingFoods ?? new List<BookingFood>()).Count();
            _logger.LogInformation("Total BookingFoods entries: {Count}", totalBookingFoods);

            if (totalBookingFoods > 0)
            {
                var sampleFood = bookingsInRange
                    .SelectMany(b => b.BookingFoods ?? new List<BookingFood>())
                    .FirstOrDefault();

                if (sampleFood?.FoodItem != null)
                {
                    _logger.LogInformation("Sample Food Item: Name={Name}, Category={Cat}, Price={Price}",
                        sampleFood.FoodItem.Name,
                        sampleFood.FoodItem.Category ?? "NULL",
                        sampleFood.UnitPrice);
                }
            }

            // Lọc bookings hợp lệ
            var validBookings = bookingsInRange
                .Where(b => b.Payment != null && b.TotalPrice.HasValue && b.TotalPrice.Value > 0)
                .ToList();

            _logger.LogInformation("Valid Bookings: {Count}", validBookings.Count);

            // ✅ TÍNH TOÁN với Normalize Category
            decimal totalTicketRevenue = 0;
            int totalTicketCount = 0;
            decimal totalFoodRevenue = 0;
            int totalFoodCount = 0;
            decimal totalDrinkRevenue = 0;
            int totalDrinkCount = 0;
            decimal totalComboRevenue = 0;
            int totalComboCount = 0;

            foreach (var booking in validBookings)
            {
                // Tính vé
                if (booking.BookingDetails != null && booking.BookingDetails.Any())
                {
                    totalTicketRevenue += booking.BookingDetails.Sum(bd => bd.Price);
                    totalTicketCount += booking.BookingDetails.Count;
                }

                // ✅ Tính đồ ăn/uống với normalize
                if (booking.BookingFoods != null && booking.BookingFoods.Any())
                {
                    foreach (var bf in booking.BookingFoods)
                    {
                        if (bf.FoodItem != null)
                        {
                            var itemRevenue = bf.UnitPrice * bf.Quantity;
                            var normalizedCategory = NormalizeCategory(bf.FoodItem.Category);

                            _logger.LogInformation("Item: {Name}, Original Cat: '{Original}', Normalized: '{Norm}', Rev: {Rev}",
                                bf.FoodItem.Name,
                                bf.FoodItem.Category ?? "NULL",
                                normalizedCategory,
                                itemRevenue);

                            switch (normalizedCategory)
                            {
                                case "Food":
                                    totalFoodRevenue += itemRevenue;
                                    totalFoodCount += bf.Quantity;
                                    break;
                                case "Drink":
                                    totalDrinkRevenue += itemRevenue;
                                    totalDrinkCount += bf.Quantity;
                                    break;
                                case "Combo":
                                    totalComboRevenue += itemRevenue;
                                    totalComboCount += bf.Quantity;
                                    break;
                            }
                        }
                    }
                }
            }

            var totalRevenue = validBookings.Sum(b => b.TotalPrice ?? 0m);
            var totalBookingCount = validBookings.Count;

            _logger.LogInformation("FINAL TOTALS:");
            _logger.LogInformation("  Ticket: {Rev} VND ({Count} tickets)", totalTicketRevenue, totalTicketCount);
            _logger.LogInformation("  Food: {Rev} VND ({Count} items)", totalFoodRevenue, totalFoodCount);
            _logger.LogInformation("  Drink: {Rev} VND ({Count} items)", totalDrinkRevenue, totalDrinkCount);
            _logger.LogInformation("  Combo: {Rev} VND ({Count} items)", totalComboRevenue, totalComboCount);

            // ✅ Revenue by Movie với normalize
            var revenueByMovie = validBookings
                .Where(b => b.Showtime?.Movie != null)
                .GroupBy(b => b.Showtime.Movie.Title ?? "Unknown")
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    TicketCount = g.Sum(b => b.BookingDetails?.Count ?? 0),
                    FoodRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Food")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    TotalRevenue = g.Sum(b => b.TotalPrice ?? 0m)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // ✅ Revenue by Date với normalize
            var revenueByDate = validBookings
                .GroupBy(b => b.BookingDate.Date)
                .Select(g => new RevenueByDateItem
                {
                    Date = g.Key,
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    TicketCount = g.Sum(b => b.BookingDetails?.Count ?? 0),
                    FoodRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Food")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    FoodCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Food")
                        .Sum(bf => bf.Quantity)),
                    DrinkRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Drink")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    DrinkCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Drink")
                        .Sum(bf => bf.Quantity)),
                    ComboRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Combo")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    ComboCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Combo")
                        .Sum(bf => bf.Quantity)),
                    TotalRevenue = g.Sum(b => b.TotalPrice ?? 0m)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // ✅ Revenue by Month với normalize
            var revenueByMonth = validBookings
                .GroupBy(b => new { b.BookingDate.Year, b.BookingDate.Month })
                .Select(g => new RevenueByMonthItem
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    TicketRevenue = g.Sum(b => b.BookingDetails?.Sum(bd => bd.Price) ?? 0m),
                    TicketCount = g.Sum(b => b.BookingDetails?.Count ?? 0),
                    FoodRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Food")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    FoodCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Food")
                        .Sum(bf => bf.Quantity)),
                    DrinkRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Drink")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    DrinkCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Drink")
                        .Sum(bf => bf.Quantity)),
                    ComboRevenue = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Combo")
                        .Sum(bf => bf.UnitPrice * bf.Quantity)),
                    ComboCount = g.Sum(b => (b.BookingFoods ?? new List<BookingFood>())
                        .Where(bf => bf.FoodItem != null && NormalizeCategory(bf.FoodItem.Category) == "Combo")
                        .Sum(bf => bf.Quantity)),
                    TotalRevenue = g.Sum(b => b.TotalPrice ?? 0m)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            // ✅ Food Statistics với normalize
            var allBookingFoods = validBookings
                .SelectMany(b => b.BookingFoods ?? new List<BookingFood>())
                .Where(bf => bf.FoodItem != null);

            var foodItemStatistics = allBookingFoods
                .GroupBy(bf => new { bf.FoodItem.Name, Category = NormalizeCategory(bf.FoodItem.Category) })
                .Select(g => new FoodItemStatistic
                {
                    Name = g.Key.Name,
                    Category = g.Key.Category,
                    TotalQuantity = g.Sum(bf => bf.Quantity),
                    TotalRevenue = g.Sum(bf => bf.UnitPrice * bf.Quantity),
                    AveragePrice = g.Average(bf => bf.UnitPrice)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // ✅ Food by Date and Category với normalize
            var revenueByFoodCategoryByDate = allBookingFoods
                .GroupBy(bf => new {
                    Date = bf.Booking.BookingDate.Date,
                    Category = NormalizeCategory(bf.FoodItem?.Category)
                })
                .Select(g => new FoodRevenueByDateItem
                {
                    Date = g.Key.Date,
                    Category = g.Key.Category,
                    Revenue = g.Sum(bf => bf.UnitPrice * bf.Quantity),
                    Quantity = g.Sum(bf => bf.Quantity)
                })
                .GroupBy(item => item.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Categories = g.ToList()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Tạo ViewBag data
            var revenueData = new
            {
                FromDate = fromDate.Value.ToString("yyyy-MM-dd"),
                ToDate = toDate.Value.ToString("yyyy-MM-dd"),
                TotalTicketRevenue = totalTicketRevenue,
                TotalTicketCount = totalTicketCount,
                TotalFoodRevenue = totalFoodRevenue,
                TotalFoodCount = totalFoodCount,
                TotalDrinkRevenue = totalDrinkRevenue,
                TotalDrinkCount = totalDrinkCount,
                TotalComboRevenue = totalComboRevenue,
                TotalComboCount = totalComboCount,
                TotalRevenue = totalRevenue,
                TotalBookingCount = totalBookingCount,
                RevenueByMovie = revenueByMovie,
                RevenueByDate = revenueByDate,
                RevenueByMonth = revenueByMonth,
                RevenueByFoodCategoryByDate = revenueByFoodCategoryByDate,
                FoodItemStatistics = foodItemStatistics
            };

            var formattedRevenueByDateForChart = revenueByDate.Select(x => new
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                TicketRevenue = x.TicketRevenue,
                TicketCount = x.TicketCount,
                FoodRevenue = x.FoodRevenue,
                FoodCount = x.FoodCount,
                DrinkRevenue = x.DrinkRevenue,
                DrinkCount = x.DrinkCount,
                ComboRevenue = x.ComboRevenue,
                ComboCount = x.ComboCount,
                TotalRevenue = x.TotalRevenue
            }).ToList();

            var formattedRevenueByMonthForChart = revenueByMonth.Select(x => new
            {
                Month = $"{x.MonthName} {x.Year}",
                TicketRevenue = x.TicketRevenue,
                TicketCount = x.TicketCount,
                FoodRevenue = x.FoodRevenue,
                FoodCount = x.FoodCount,
                DrinkRevenue = x.DrinkRevenue,
                DrinkCount = x.DrinkCount,
                ComboRevenue = x.ComboRevenue,
                ComboCount = x.ComboCount,
                TotalRevenue = x.TotalRevenue
            }).ToList();

            ViewBag.RevenueData = revenueData;
            ViewBag.FormattedRevenueByDate = JsonConvert.SerializeObject(formattedRevenueByDateForChart);
            ViewBag.FormattedRevenueByMonth = JsonConvert.SerializeObject(formattedRevenueByMonthForChart);
            ViewBag.FormattedFoodByDate = JsonConvert.SerializeObject(revenueByFoodCategoryByDate);

            _logger.LogInformation("=== END REVENUE STATISTICS DEBUG ===");

            return View();
        }
        // ==================== HELPER CLASSES ====================


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
                return Json(new { success = false, message = "Invalid data." });

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
            return Json(new { success = true, message = "Food item added successfully!" });
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
                return Json(new { success = false, message = "Invalid data." });

            var existing = await _context.FoodItems.FindAsync(foodItemId);
            if (existing == null)
                return Json(new { success = false, message = "Food item not found." });

            existing.Name = name.Trim();
            existing.Size = size;
            existing.Price = price;
            existing.Category = category;

            if (imageFile != null && imageFile.Length > 0)
            {
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
                return Json(new { success = true, message = "Food item updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating food item {FoodItemId}", foodItemId);
                return Json(new { success = false, message = "Error saving data: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFood(int id)
        {
            var food = await _context.FoodItems.FindAsync(id);
            if (food == null)
                return Json(new { success = false, message = "Food item not found." });

            if (!string.IsNullOrEmpty(food.ImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", food.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.FoodItems.Remove(food);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Food item deleted successfully!" });
        }
    }

    public class RevenueByDateItem
    {
        public DateTime Date { get; set; }
        public decimal TicketRevenue { get; set; }
        public int TicketCount { get; set; }
        public decimal FoodRevenue { get; set; }
        public int FoodCount { get; set; }
        public decimal DrinkRevenue { get; set; }
        public int DrinkCount { get; set; }
        public decimal ComboRevenue { get; set; }
        public int ComboCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class RevenueByMonthItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public decimal TicketRevenue { get; set; }
        public int TicketCount { get; set; }
        public decimal FoodRevenue { get; set; }
        public int FoodCount { get; set; }
        public decimal DrinkRevenue { get; set; }
        public int DrinkCount { get; set; }
        public decimal ComboRevenue { get; set; }
        public int ComboCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class FoodRevenueByDateItem
    {
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public decimal Revenue { get; set; }
        public int Quantity { get; set; }
    }

    public class FoodItemStatistic
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AveragePrice { get; set; }
    }
}