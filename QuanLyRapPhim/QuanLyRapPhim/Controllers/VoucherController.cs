// Updated VoucherController.cs - Aligned with provided Voucher model.
// Added actions for Available vouchers (all active vouchers) and MyVouchers (user's claimed vouchers).
// Introduced UserVoucher model for tracking claimed vouchers per user (many-to-many relationship).
// To implement UserVoucher, add the following to Models/UserVoucher.cs:
// public class UserVoucher {
//     public int Id { get; set; }
//     public string UserId { get; set; }
//     public int VoucherId { get; set; }
//     public DateTime ClaimDate { get; set; }
//     public bool IsUsed { get; set; } = false;
//     public virtual Voucher Voucher { get; set; }
//     public virtual ApplicationUser User { get; set; } // Assuming Identity for ApplicationUser
// }
// Then in DBContext.cs: public DbSet<UserVoucher> UserVouchers { get; set; }
// In OnModelCreating: builder.Entity<UserVoucher>().HasKey(uv => uv.Id);
// builder.Entity<UserVoucher>().HasOne(uv => uv.Voucher).WithMany().HasForeignKey(uv => uv.VoucherId);
// builder.Entity<UserVoucher>().HasOne(uv => uv.User).WithMany().HasForeignKey(uv => uv.UserId);
// Also added Claim action to allow users to claim a voucher.
// UsedCount increments only when actually used (e.g., in Booking), not on claim. Adjust if needed.

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Controllers
{
    // Removed class-level [Authorize] to allow user actions
    public class VouchersController : Controller
    {
        private readonly DBContext _context;
        private readonly UserManager<User> _userManager; // Assuming Identity; adjust if different user system

        public VouchersController(DBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Vouchers (Admin only - List all vouchers with filters)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchCode = null, bool? isActive = null)
        {
            var vouchers = _context.Vouchers
                .Include(v => v.Bookings) // Eager load to count usage if needed
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchCode))
            {
                vouchers = vouchers.Where(v => v.Code.Contains(searchCode));
            }

            if (isActive.HasValue)
            {
                vouchers = vouchers.Where(v => v.IsActive == isActive.Value);
            }

            // Filter expired (optional, can add to view filter)
            vouchers = vouchers.Where(v => v.ExpiryDate >= DateTime.Now || v.IsActive);

            var result = await vouchers.ToListAsync();
            return View(result);
        }

        // GET: Vouchers/Details/5 (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.Bookings) // To show actual usage
                .FirstOrDefaultAsync(m => m.VoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        // GET: Vouchers/Create (Admin only)
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Vouchers/Create (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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

        // GET: Vouchers/Edit/5 (Admin only)
        [Authorize(Roles = "Admin")]
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

        // POST: Vouchers/Edit/5 (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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

        // GET: Vouchers/Delete/5 (Admin only)
        [Authorize(Roles = "Admin")]
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

        // POST: Vouchers/Delete/5 (Admin only)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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

        // NEW: GET: Vouchers/Available (User - List all available active vouchers)
        [Authorize] // Requires login, but not admin
        public async Task<IActionResult> Available()
        {
            var availableVouchers = await _context.Vouchers
                .Where(v => v.IsActive && v.ExpiryDate >= DateTime.Now && (v.UsageLimit == 0 || v.UsedCount < v.UsageLimit))
                .ToListAsync();

            return View(availableVouchers); // Create Views/Vouchers/Available.cshtml to display list with Claim button
        }

        // NEW: GET: Vouchers/MyVouchers (User - List user's claimed vouchers)
        [Authorize]
        public async Task<IActionResult> MyVouchers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId && !uv.IsUsed && uv.Voucher.ExpiryDate >= DateTime.Now && uv.Voucher.IsActive)
                .Select(uv => uv.Voucher)
                .ToListAsync();

            return View(myVouchers); // Create Views/Vouchers/MyVouchers.cshtml to display list
        }

        // NEW: POST: Vouchers/Claim/5 (User - Claim a voucher)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null || !voucher.IsActive || voucher.ExpiryDate < DateTime.Now || (voucher.UsageLimit > 0 && voucher.UsedCount >= voucher.UsageLimit))
            {
                TempData["ErrorMessage"] = "Voucher không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction(nameof(Available));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (await _context.UserVouchers.AnyAsync(uv => uv.UserId == userId && uv.VoucherId == id))
            {
                TempData["ErrorMessage"] = "Bạn đã claim voucher này rồi.";
                return RedirectToAction(nameof(Available));
            }

            var userVoucher = new UserVoucher
            {
                UserId = userId,
                VoucherId = id,
                ClaimDate = DateTime.Now,
                IsUsed = false
            };

            _context.UserVouchers.Add(userVoucher);
            // Note: UsedCount increases when used in Booking, not on claim.
            // If you want to limit claims per voucher, add logic here or adjust model.
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Claim voucher '{voucher.Code}' thành công!";
            return RedirectToAction(nameof(MyVouchers));
        }

        private bool VoucherExists(int id)
        {
            return _context.Vouchers.Any(e => e.VoucherId == id);
        }
    }
}