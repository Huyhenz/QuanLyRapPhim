#nullable disable
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Data;
using Microsoft.EntityFrameworkCore;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly DBContext _context;
        private readonly SignInManager<User> _signInManager;

        public ConfirmEmailModel(UserManager<User> userManager, DBContext context, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _context = context;
            _signInManager = signInManager;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string token, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "Token xác nhận không hợp lệ.";
                return Page();
            }

            // Tìm PendingUser theo token
            var pendingUser = await _context.PendingUsers
                .FirstOrDefaultAsync(p => p.ConfirmationToken == token);

            if (pendingUser == null)
            {
                StatusMessage = "Link xác nhận không hợp lệ hoặc đã hết hạn. Vui lòng đăng ký lại.";
                return Page();
            }

            // Kiểm tra token hết hạn
            if (pendingUser.ExpiresAt.HasValue && pendingUser.ExpiresAt.Value < DateTime.Now)
            {
                _context.PendingUsers.Remove(pendingUser);
                await _context.SaveChangesAsync();
                StatusMessage = "Link xác nhận đã hết hạn. Vui lòng đăng ký lại.";
                return Page();
            }

            // Kiểm tra email đã tồn tại chưa (trường hợp đã được xác nhận trước đó)
            var existingUser = await _userManager.FindByEmailAsync(pendingUser.Email);
            if (existingUser != null)
            {
                // Xóa pending user và thông báo
                _context.PendingUsers.Remove(pendingUser);
                await _context.SaveChangesAsync();
                StatusMessage = "Email này đã được xác nhận trước đó. Bạn có thể đăng nhập ngay.";
                return Page();
            }

            try
            {
                // Tạo User thực sự từ PendingUser
                var user = new User
                {
                    FullName = pendingUser.FullName,
                    DateOfBirth = pendingUser.DateOfBirth,
                    UserName = pendingUser.Email,
                    Email = pendingUser.Email,
                    EmailConfirmed = true, // Đã xác nhận email
                    PasswordHash = pendingUser.PasswordHash
                };

                var result = await _userManager.CreateAsync(user);
                
                if (result.Succeeded)
                {
                    // Thêm role User
                    await _userManager.AddToRoleAsync(user, "User");

                    // Xóa PendingUser
                    _context.PendingUsers.Remove(pendingUser);
                    await _context.SaveChangesAsync();

                    StatusMessage = "Email đã được xác nhận thành công. Tài khoản của bạn đã được kích hoạt.";
                }
                else
                {
                    StatusMessage = "Lỗi khi tạo tài khoản: " + string.Join(", ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi xác nhận email: " + ex.Message;
            }

            return Page();
        }
    }
}

