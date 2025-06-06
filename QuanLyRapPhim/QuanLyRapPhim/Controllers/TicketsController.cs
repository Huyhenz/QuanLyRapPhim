﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage; // For transaction management

namespace QuanLyRapPhim.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager;

        public TicketsController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tickets/SelectRoomAndSeat
        public async Task<IActionResult> SelectRoomAndSeat(int showtimeId)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .ThenInclude(r => r.Seats)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
            {
                return NotFound();
            }

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            foreach (var seat in showtime.Room.Seats)
            {
                seat.Status = bookedSeats.Contains(seat.SeatId) ? "Đã đặt" : "Trống";
            }

            return View(showtime);
        }

        // POST: Tickets/ConfirmBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ConfirmBooking(int showtimeId, List<int> selectedSeats)
        {
            if (selectedSeats == null || !selectedSeats.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ghế.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .ThenInclude(r => r.Seats)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
            {
                return NotFound();
            }

            var bookedSeats = await _context.BookingDetails
                .Where(bd => bd.Booking.ShowtimeId == showtimeId)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            foreach (var seatId in selectedSeats)
            {
                if (bookedSeats.Contains(seatId))
                {
                    TempData["ErrorMessage"] = "Một hoặc nhiều ghế đã được đặt. Vui lòng chọn ghế khác.";
                    return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var totalRows = (int)Math.Ceiling((double)showtime.Room.Seats.Count / 10);
            char lastRowLabel = (char)('A' + totalRows - 1);
            const decimal normalPrice = 50000;
            const decimal couplePrice = 100000;

            var booking = new Booking
            {
                ShowtimeId = showtimeId,
                BookingDate = DateTime.Now,
                UserId = user?.Id,
                BookingDetails = new List<BookingDetail>()
            };

            foreach (var seatId in selectedSeats)
            {
                var seat = showtime.Room.Seats.FirstOrDefault(s => s.SeatId == seatId);
                if (seat != null)
                {
                    decimal price = seat.SeatNumber.StartsWith(lastRowLabel.ToString()) ? couplePrice : normalPrice;
                    booking.BookingDetails.Add(new BookingDetail
                    {
                        SeatId = seatId,
                        Price = price
                    });
                }
            }

            booking.TotalPrice = booking.BookingDetails.Sum(bd => bd.Price);

            // Bắt đầu giao dịch
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Thêm Booking vào cơ sở dữ liệu
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    // Tạo bản ghi Payment tương ứng
                    var payment = new Payment
                    {
                        BookingId = booking.BookingId,
                        Amount = booking.TotalPrice,
                        PaymentDate = booking.BookingDate,
                        PaymentMethod = "VNPay",
                        PaymentStatus = "Completed"
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Cập nhật trạng thái ghế thành "Đã đặt"
                    foreach (var seatId in selectedSeats)
                    {
                        var seat = await _context.Seats.FindAsync(seatId);
                        if (seat != null)
                        {
                            seat.Status = "Đã đặt";
                        }
                    }
                    await _context.SaveChangesAsync();

                    // Commit giao dịch
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    // Nếu có lỗi, rollback giao dịch
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xử lý đặt vé. Vui lòng thử lại.";
                    return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
                }
            }

            // Chuyển hướng sang trang thanh toán
            return RedirectToAction("Create", "Payments", new { bookingId = booking.BookingId });
        }

        // POST: Tickets/CancelBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId, int showtimeId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đặt vé.";
                return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
            }

            // Lấy danh sách SeatId từ BookingDetails
            var seatIds = booking.BookingDetails.Select(bd => bd.SeatId).ToList();

            // Xóa BookingDetails và Booking
            _context.BookingDetails.RemoveRange(booking.BookingDetails);
            _context.Bookings.Remove(booking);

            // Cập nhật trạng thái ghế về "Trống"
            foreach (var seatId in seatIds)
            {
                var seat = await _context.Seats.FindAsync(seatId);
                if (seat != null)
                {
                    seat.Status = "Trống";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã hủy đặt vé. Vui lòng chọn lại ghế.";
            return RedirectToAction("SelectRoomAndSeat", new { showtimeId });
        }
    }
}