using System;
using System.Collections.Generic;
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
            _context = context;
        }
        public IActionResult Search(string searchTitle, string startDate, string endDate, string genre)
        {

            var showtimes = _context.Showtimes.Include(s => s.Movie).AsQueryable();

            // Nếu không có điều kiện tìm kiếm, hiển thị toàn bộ lịch chiếu
            if (string.IsNullOrEmpty(searchTitle) && string.IsNullOrEmpty(startDate) && string.IsNullOrEmpty(endDate) && string.IsNullOrEmpty(genre))
            {
                return View("Index", showtimes.ToList());
            }

            if (!string.IsNullOrEmpty(searchTitle))
            {
                showtimes = showtimes.Where(s => s.Movie.Title.Contains(searchTitle));
            }

            // Chuyển đổi & lọc theo khoảng ngày chiếu
            DateTime? start = DateTime.TryParse(startDate, out DateTime parsedStart) ? parsedStart.Date : null;
            DateTime? end = DateTime.TryParse(endDate, out DateTime parsedEnd) ? parsedEnd.Date : null;

            if (start.HasValue && end.HasValue)
            {
                showtimes = showtimes.Where(s => (s.Date.Date.CompareTo(start.Value) >= 0) && (s.Date.Date.CompareTo(end.Value) <= 0));
            }

            if (!string.IsNullOrEmpty(genre))
            {
                showtimes = showtimes.Where(s => s.Movie.Genre.Contains(genre));
            }

            return View("Index", showtimes.ToList());
        }
        // GET: Showtimes/SelectShowtime?movieId=5
        public async Task<IActionResult> SelectShowtime(int movieId)
        {
            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Where(s => s.MovieId == movieId)
                .ToListAsync();

            if (showtimes == null || !showtimes.Any())
            {
                ViewBag.ErrorMessage = "Hiện tại chưa có lịch chiếu cho phim này.";
            }

            ViewBag.MovieId = movieId;
            return View(showtimes);
        }

        // GET: Showtimes
        public async Task<IActionResult> Index()
        {
            var dBContext = _context.Showtimes.Include(s => s.Movie);
            return View(await dBContext.ToListAsync());
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
                .FirstOrDefaultAsync(m => m.ShowtimeId == id);
            if (showtime == null)
            {
                return NotFound();
            }

            return View(showtime);
        }
        [Authorize(Roles = "Admin")]
        // GET: Showtimes/Create
        public IActionResult Create()
        {
            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title");
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName");
            return View();
        }

        // POST: Showtimes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ShowtimeId,Title,MovieId,Poster,StartTime,Date,RoomId")] Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra MovieId có tồn tại không
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        ModelState.AddModelError("MovieId", "Phim không tồn tại.");
                        ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
                        return View(showtime);
                    }

                    // Kiểm tra RoomId có tồn tại không
                    if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        ModelState.AddModelError("RoomId", "Phòng không tồn tại.");
                        ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
                        ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
                        return View(showtime);
                    }

                    _context.Add(showtime);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Lỗi khi tạo lịch chiếu: {ex.Message}");
                }
            }
            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
            return View(showtime);
        }

        // GET: Showtimes/Edit/5
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
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    // Kiểm tra MovieId có tồn tại không
                    if (!_context.Movies.Any(m => m.MovieId == showtime.MovieId))
                    {
                        ModelState.AddModelError("MovieId", "Phim không tồn tại.");
                        ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
                        return View(showtime);
                    }

                    // Kiểm tra RoomId có tồn tại không
                    if (!_context.Rooms.Any(r => r.RoomId == showtime.RoomId))
                    {
                        ModelState.AddModelError("RoomId", "Phòng không tồn tại.");
                        ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
                        ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
                        return View(showtime);
                    }

                    _context.Update(showtime);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShowtimeExists(showtime.ShowtimeId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Lỗi khi cập nhật lịch chiếu: {ex.Message}");
                }
            }
            ViewData["MovieId"] = new SelectList(_context.Movies, "MovieId", "Title", showtime.MovieId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", showtime.RoomId);
            return View(showtime);
        }

        // GET: Showtimes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
            {
                return NotFound();
            }

            try
            {
                // Kiểm tra xem Showtime có Booking liên quan không
                if (_context.Bookings.Any(b => b.ShowtimeId == id))
                {
                    TempData["ErrorMessage"] = "Không thể xóa lịch chiếu này vì đã có vé được đặt.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Showtimes.Remove(showtime);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa lịch chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa lịch chiếu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ShowtimeExists(int id)
        {
            return _context.Showtimes.Any(e => e.ShowtimeId == id);
        }
    }
}
