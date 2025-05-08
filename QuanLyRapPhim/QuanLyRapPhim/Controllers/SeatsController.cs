using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    public class SeatsController : Controller
    {
        private readonly DBContext _context;

        public SeatsController(DBContext context)
        {
            _context = context;
        }

        // GET: Seats
        public async Task<IActionResult> Index()
        {
            var dBContext = _context.Seats.Include(s => s.Room);
            return View(await dBContext.ToListAsync());
        }

        // GET: Seats/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seat = await _context.Seats
                .Include(s => s.Room)
                .FirstOrDefaultAsync(m => m.SeatId == id);
            if (seat == null)
            {
                return NotFound();
            }

            return View(seat);
        }

        // GET: Seats/Create
        public IActionResult Create()
        {
            // Hiển thị dropdown list các phòng, hiển thị RoomName thay vì RoomId
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName");
            return View();
        }

        // POST: Seats/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int RoomId)
        {
            // Kiểm tra xem RoomId có hợp lệ không
            var room = await _context.Rooms.FindAsync(RoomId);
            if (room == null)
            {
                ModelState.AddModelError("RoomId", "Phòng không tồn tại.");
                ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", RoomId);
                return View();
            }

            // Kiểm tra xem phòng đã có ghế chưa
            var existingSeats = await _context.Seats.Where(s => s.RoomId == RoomId).ToListAsync();
            if (existingSeats.Any())
            {
                ModelState.AddModelError("", "Phòng này đã có ghế. Vui lòng xóa các ghế hiện tại trước khi tạo mới.");
                ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", RoomId);
                return View();
            }

            // Tạo ghế tự động dựa trên TotalSeats của phòng
            int seatsPerRow = 10; // Mỗi hàng có 10 ghế, nhưng đánh số từ 0-9
            int totalSeats = room.Capacity;
            int totalRows = (int)Math.Ceiling((double)totalSeats / seatsPerRow);

            var seatsToAdd = new List<Seat>();
            for (int row = 0; row < totalRows; row++)
            {
                char rowLabel = (char)('A' + row); // A, B, C, ...
                int seatsInThisRow = Math.Min(seatsPerRow, totalSeats - (row * seatsPerRow));

                for (int seatNum = 0; seatNum < seatsInThisRow; seatNum++) // Đánh số từ 0-9
                {
                    var seat = new Seat
                    {
                        RoomId = RoomId,
                        SeatNumber = $"{rowLabel}{seatNum}",
                        Status = "Trống", // Trạng thái mặc định
                        Room = null,
                        BookingDetails = null
                    };
                    seatsToAdd.Add(seat);
                }
            }

            // Thêm tất cả ghế vào cơ sở dữ liệu
            _context.Seats.AddRange(seatsToAdd);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo thành công {seatsToAdd.Count} ghế cho phòng {room.RoomName}!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Seats/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seat = await _context.Seats.FindAsync(id);
            if (seat == null)
            {
                return NotFound();
            }
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", seat.RoomId);
            return View(seat);
        }

        // POST: Seats/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SeatId,RoomId,SeatNumber,Status")] Seat seat)
        {
            if (id != seat.SeatId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(seat);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SeatExists(seat.SeatId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoomId"] = new SelectList(_context.Rooms, "RoomId", "RoomName", seat.RoomId);
            return View(seat);
        }

        // GET: Seats/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seat = await _context.Seats
                .Include(s => s.Room)
                .FirstOrDefaultAsync(m => m.SeatId == id);
            if (seat == null)
            {
                return NotFound();
            }

            return View(seat);
        }

        // POST: Seats/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat != null)
            {
                _context.Seats.Remove(seat);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SeatExists(int id)
        {
            return _context.Seats.Any(e => e.SeatId == id);
        }
    }
}