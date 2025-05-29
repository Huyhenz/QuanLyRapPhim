using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    [Authorize]
    public class ShowtimesController : Controller
    {
        private readonly DBContext _context;

        public ShowtimesController(DBContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private DateTime GetCurrentTime()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, timeZone);
        }

        // GET: Showtimes?movieId=4
        public async Task<IActionResult> Index(int? movieId, string searchTitle, DateTime? startDate, DateTime? endDate, string genre)
        {
            var now = GetCurrentTime();
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .AsNoTracking()
                .AsQueryable();

            // Filter by movieId if provided
            if (movieId.HasValue)
            {
                showtimes = showtimes.Where(s => s.MovieId == movieId.Value);
            }

            if (!User.IsInRole("Admin"))
            {
                showtimes = showtimes.Where(s =>
                    s.Date.Date > now.Date ||
                    (s.Date.Date == now.Date && s.StartTime >= now.TimeOfDay));
            }

            // Apply search filters
            if (!string.IsNullOrEmpty(searchTitle))
            {
                showtimes = showtimes.Where(s => s.Movie.Title.Contains(searchTitle, StringComparison.OrdinalIgnoreCase));
            }

            if (startDate.HasValue)
            {
                showtimes = showtimes.Where(s => s.Date.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                showtimes = showtimes.Where(s => s.Date.Date <= endDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(genre))
            {
                showtimes = showtimes.Where(s => s.Movie.Genre.Contains(genre, StringComparison.OrdinalIgnoreCase));
            }

            var result = await showtimes.ToListAsync();

            // Set ViewBag for movieId and movie title
            ViewBag.MovieId = movieId;
            if (movieId.HasValue)
            {
                var movie = await _context.Movies.FindAsync(movieId.Value);
                ViewBag.MovieTitle = movie?.Title ?? "Phim không xác định";
            }
            else
            {
                ViewBag.MovieTitle = "Tất cả phim";
            }

            if (!result.Any() && movieId.HasValue)
            {
                ViewBag.ErrorMessage = "Hiện tại chưa có lịch chiếu khả dụng cho phim này.";
            }

            return View(result);
        }

        // GET: Showtimes/SelectShowtime?movieId=5
        public async Task<IActionResult> SelectShowtime(int movieId)
        {
            var now = GetCurrentTime();
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .AsNoTracking()
                .Where(s => s.MovieId == movieId)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                showtimes = showtimes.Where(s =>
                    s.Date.Date > now.Date ||
                    (s.Date.Date == now.Date && s.StartTime >= now.TimeOfDay));
            }

            var result = await showtimes.ToListAsync();

            if (result == null || !result.Any())
            {
                ViewBag.ErrorMessage = "Hiện tại chưa có lịch chiếu khả dụng cho phim này.";
            }

            ViewBag.MovieId = movieId;
            var movie = await _context.Movies.FindAsync(movieId);
            ViewBag.MovieTitle = movie?.Title ?? "Phim không xác định";

            return View("Index", result); // Use Index view for consistency
        }

        // POST: Showtimes/Search
        public async Task<IActionResult> Search(int? movieId, string searchTitle, string startDate, string endDate, string genre)
        {
            var now = GetCurrentTime();
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .AsNoTracking()
                .AsQueryable();

            // Preserve movieId filter
            if (movieId.HasValue)
            {
                showtimes = showtimes.Where(s => s.MovieId == movieId.Value);
            }

            if (!User.IsInRole("Admin"))
            {
                showtimes = showtimes.Where(s =>
                    s.Date.Date > now.Date ||
                    (s.Date.Date == now.Date && s.StartTime >= now.TimeOfDay));
            }

            if (!string.IsNullOrEmpty(searchTitle))
            {
                showtimes = showtimes.Where(s => s.Movie.Title.Contains(searchTitle, StringComparison.OrdinalIgnoreCase));
            }

            if (DateTime.TryParse(startDate, out DateTime parsedStart))
            {
                showtimes = showtimes.Where(s => s.Date.Date >= parsedStart.Date);
            }

            if (DateTime.TryParse(endDate, out DateTime parsedEnd))
            {
                showtimes = showtimes.Where(s => s.Date.Date <= parsedEnd.Date);
            }

            if (!string.IsNullOrEmpty(genre))
            {
                showtimes = showtimes.Where(s => s.Movie.Genre.Contains(genre, StringComparison.OrdinalIgnoreCase));
            }

            var result = await showtimes.ToListAsync();

            // Set ViewBag for movieId and movie title
            ViewBag.MovieId = movieId;
            if (movieId.HasValue)
            {
                var movie = await _context.Movies.FindAsync(movieId.Value);
                ViewBag.MovieTitle = movie?.Title ?? "Phim không xác định";
            }
            else
            {
                ViewBag.MovieTitle = "Tất cả phim";
            }

            if (!result.Any())
            {
                ViewBag.ErrorMessage = movieId.HasValue
                    ? "Hiện tại chưa có lịch chiếu khả dụng cho phim này."
                    : "Không tìm thấy lịch chiếu phù hợp.";
            }

            return View("Index", result);
        }

        // GET: Showtimes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ShowtimeId == id);

            if (showtime == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin"))
            {
                var now = GetCurrentTime();
                if (showtime.Date.Date < now.Date ||
                    (showtime.Date.Date == now.Date && showtime.StartTime < now.TimeOfDay))
                {
                    return NotFound();
                }
            }

            return View(showtime);
        }

        // GET: Showtimes/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title");
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName");
            return View();
        }

        // POST: Showtimes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ShowtimeId,Title,MovieId,Poster,StartTime,Date,RoomId")] Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        ModelState.AddModelError("MovieId", "Phim không tồn tại.");
                    }
                    else if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        ModelState.AddModelError("RoomId", "Phòng không tồn tại.");
                    }
                    else
                    {
                        _context.Add(showtime);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Tạo lịch chiếu thành công!";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi tạo lịch chiếu: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
            return View(showtime);
        }

        // GET: Showtimes/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
            {
                return NotFound();
            }

            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
            return View(showtime);
        }

        // POST: Showtimes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("ShowtimeId,Title,MovieId,Poster,StartTime,Date,RoomId")] Showtime showtime)
        {
            if (id != showtime.ShowtimeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        ModelState.AddModelError("MovieId", "Phim không tồn tại.");
                    }
                    else if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        ModelState.AddModelError("RoomId", "Phòng không tồn tại.");
                    }
                    else
                    {
                        _context.Update(showtime);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Cập nhật lịch chiếu thành công!";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShowtimeExists(showtime.ShowtimeId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Lỗi khi cập nhật lịch chiếu: {ex.Message}";
                }
            }

            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
            return View(showtime);
        }

        // GET: Showtimes/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(m => m.ShowtimeId == id);
            if (showtime == null)
            {
                return NotFound();
            }

            return View(showtime);
        }

        // POST: Showtimes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
            {
                return NotFound();
            }

            try
            {
                if (_context.Bookings.Any(b => b.ShowtimeId == id))
                {
                    TempData["ErrorMessage"] = "Không thể xóa lịch chiếu vì đã có vé được đặt.";
                }
                else
                {
                    _context.Showtimes.Remove(showtime);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Xóa lịch chiếu thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa lịch chiếu: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ShowtimeExists(int id)
        {
            return _context.Showtimes.Any(e => e.ShowtimeId == id);
        }
    }
}