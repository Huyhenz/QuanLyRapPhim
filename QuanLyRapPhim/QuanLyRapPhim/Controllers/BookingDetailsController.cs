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
    public class BookingDetailsController : Controller
    {
        private readonly DBContext _context;

        public BookingDetailsController(DBContext context)
        {
            _context = context;
        }

        // GET: BookingDetails
        public async Task<IActionResult> Index()
        {
            var dBContext = _context.BookingDetails.Include(b => b.Booking).Include(b => b.Seat);
            return View(await dBContext.ToListAsync());
        }

        // GET: BookingDetails/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookingDetail = await _context.BookingDetails
                .Include(b => b.Booking)
                .Include(b => b.Seat)
                .FirstOrDefaultAsync(m => m.BookingDetailId == id);
            if (bookingDetail == null)
            {
                return NotFound();
            }

            return View(bookingDetail);
        }

        // GET: BookingDetails/Create
        public IActionResult Create()
        {
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId");
            ViewData["SeatId"] = new SelectList(_context.Seats, "SeatId", "SeatId");
            return View();
        }

        // POST: BookingDetails/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookingDetailId,BookingId,SeatId,Price")] BookingDetail bookingDetail)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bookingDetail);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId", bookingDetail.BookingId);
            ViewData["SeatId"] = new SelectList(_context.Seats, "SeatId", "SeatId", bookingDetail.SeatId);
            return View(bookingDetail);
        }

        // GET: BookingDetails/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookingDetail = await _context.BookingDetails.FindAsync(id);
            if (bookingDetail == null)
            {
                return NotFound();
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId", bookingDetail.BookingId);
            ViewData["SeatId"] = new SelectList(_context.Seats, "SeatId", "SeatId", bookingDetail.SeatId);
            return View(bookingDetail);
        }

        // POST: BookingDetails/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BookingDetailId,BookingId,SeatId,Price")] BookingDetail bookingDetail)
        {
            if (id != bookingDetail.BookingDetailId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookingDetail);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookingDetailExists(bookingDetail.BookingDetailId))
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
            ViewData["BookingId"] = new SelectList(_context.Bookings, "BookingId", "BookingId", bookingDetail.BookingId);
            ViewData["SeatId"] = new SelectList(_context.Seats, "SeatId", "SeatId", bookingDetail.SeatId);
            return View(bookingDetail);
        }

        // GET: BookingDetails/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookingDetail = await _context.BookingDetails
                .Include(b => b.Booking)
                .Include(b => b.Seat)
                .FirstOrDefaultAsync(m => m.BookingDetailId == id);
            if (bookingDetail == null)
            {
                return NotFound();
            }

            return View(bookingDetail);
        }

        // POST: BookingDetails/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookingDetail = await _context.BookingDetails.FindAsync(id);
            if (bookingDetail != null)
            {
                _context.BookingDetails.Remove(bookingDetail);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookingDetailExists(int id)
        {
            return _context.BookingDetails.Any(e => e.BookingDetailId == id);
        }
    }
}
