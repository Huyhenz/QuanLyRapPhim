using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VouchersController : Controller
    {
        private readonly DBContext _context;

        public VouchersController(DBContext context)
        {
            _context = context;
        }

        // GET: Vouchers
        public async Task<IActionResult> Index(string searchCode = null, bool? isActive = null)
        {
            var vouchers = _context.Vouchers
                .Include(v => v.Bookings) // Eager load để đếm usage nếu cần
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchCode))
            {
                vouchers = vouchers.Where(v => v.Code.Contains(searchCode));
            }

            if (isActive.HasValue)
            {
                vouchers = vouchers.Where(v => v.IsActive == isActive.Value);
            }

            // Filter expired (optional, có thể thêm vào view filter)
            vouchers = vouchers.Where(v => v.ExpiryDate >= DateTime.Now || v.IsActive);

            var result = await vouchers.ToListAsync();
            return View(result);
        }

        // GET: Vouchers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.Bookings) // Để hiển thị số lần dùng thực tế
                .FirstOrDefaultAsync(m => m.VoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        // GET: Vouchers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Vouchers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VoucherId,Code,DiscountAmount,DiscountPercentage,ExpiryDate,IsActive,UsageLimit,UsedCount")] Voucher voucher)
        {
            if (ModelState.IsValid)
            {
                _context.Add(voucher);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Tạo voucher '{voucher.Code}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(voucher);
        }

        // GET: Vouchers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null)
            {
                return NotFound();
            }
            return View(voucher);
        }

        // POST: Vouchers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VoucherId,Code,DiscountAmount,DiscountPercentage,ExpiryDate,IsActive,UsageLimit,UsedCount")] Voucher voucher)
        {
            if (id != voucher.VoucherId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(voucher);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Cập nhật voucher '{voucher.Code}' thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VoucherExists(voucher.VoucherId))
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
                    TempData["ErrorMessage"] = $"Lỗi khi cập nhật voucher: {ex.Message}";
                }
                return RedirectToAction(nameof(Index));
            }
            return View(voucher);
        }

        // GET: Vouchers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(m => m.VoucherId == id);
            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        // POST: Vouchers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.Bookings)
                .FirstOrDefaultAsync(v => v.VoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            if (voucher.UsedCount > 0 || voucher.Bookings.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa voucher '{voucher.Code}' vì đã được sử dụng.";
                return RedirectToAction(nameof(Index));
            }

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Xóa voucher '{voucher.Code}' thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool VoucherExists(int id)
        {
            return _context.Vouchers.Any(e => e.VoucherId == id);
        }
    }
}