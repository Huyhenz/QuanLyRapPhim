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
    public class RoomsController : Controller
    {
        private readonly DBContext _context;

        public RoomsController(DBContext context)
        {
            _context = context;
        }

        // GET: Rooms
        public async Task<IActionResult> Index()
        {
            return View(await _context.Rooms.ToListAsync());
        }

        // GET: Rooms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .FirstOrDefaultAsync(m => m.RoomId == id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // GET: Rooms/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,RoomName,Capacity")] Room room)
        {
            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Rooms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }
            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RoomId,RoomName,Capacity")] Room room)
        {
            if (id != room.RoomId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.RoomId))
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
            return View(room);
        }

        // GET: Rooms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .FirstOrDefaultAsync(m => m.RoomId == id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Seats) // Bao gồm ghế liên quan
                .Include(r => r.Showtimes) // Bao gồm lịch chiếu liên quan
                .ThenInclude(s => s.Bookings) // Bao gồm đặt vé liên quan
                .ThenInclude(b => b.BookingDetails) // Bao gồm chi tiết đặt vé
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            // Xóa các BookingDetails liên quan
            foreach (var booking in room.Showtimes.SelectMany(s => s.Bookings))
            {
                if (booking.BookingDetails != null && booking.BookingDetails.Any())
                {
                    _context.BookingDetails.RemoveRange(booking.BookingDetails);
                }
            }

            // Xóa các Booking liên quan
            var bookingsToRemove = room.Showtimes.SelectMany(s => s.Bookings).ToList();
            if (bookingsToRemove.Any())
            {
                _context.Bookings.RemoveRange(bookingsToRemove);
            }

            // Xóa các Showtimes liên quan
            if (room.Showtimes.Any())
            {
                _context.Showtimes.RemoveRange(room.Showtimes);
            }

            // Xóa các Seats liên quan
            if (room.Seats.Any())
            {
                _context.Seats.RemoveRange(room.Seats);
            }

            // Xóa Room
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa thành công phòng {room.RoomName}!";
            return RedirectToAction(nameof(Index));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.RoomId == id);
        }
    }
}