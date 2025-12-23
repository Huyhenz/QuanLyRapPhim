#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(UserManager<User> userManager, IEmailSender emailSender, ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                
                // Kiểm tra email có tồn tại trong DB không
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản với email này không tồn tại trong hệ thống.");
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", Input.Email);
                    return Page();
                }

                // Cho phép đặt lại mật khẩu cho tất cả tài khoản đã tồn tại trong DB (không cần kiểm tra email đã xác nhận)
                // Tạo token reset password
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code = code, email = Input.Email },
                    protocol: Request.Scheme);

                try
                {
                    // Gửi email reset password
                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Đặt lại mật khẩu - CinemaX",
                        $@"
                        <html>
                        <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                            <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 10px; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                                <h2 style='color: #e50914; text-align: center;'>Đặt lại mật khẩu</h2>
                                <p style='color: #333; font-size: 16px;'>Xin chào <strong>{user.FullName}</strong>,</p>
                                <p style='color: #333; font-size: 16px;'>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản CinemaX của mình. Vui lòng nhấp vào nút bên dưới để đặt lại mật khẩu:</p>
                                <div style='text-align: center; margin: 30px 0;'>
                                    <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #e50914; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>Đặt lại mật khẩu</a>
                                </div>
                                <p style='color: #666; font-size: 14px;'>Hoặc copy và dán link sau vào trình duyệt:</p>
                                <p style='color: #666; font-size: 12px; word-break: break-all;'>{HtmlEncoder.Default.Encode(callbackUrl)}</p>
                                <p style='color: #666; font-size: 14px; margin-top: 30px;'>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Mật khẩu của bạn sẽ không thay đổi.</p>
                                <p style='color: #ff9800; font-size: 12px; margin-top: 20px;'><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 1 giờ.</p>
                                <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                                <p style='color: #999; font-size: 12px; text-align: center;'>© 2024 CinemaX. All rights reserved.</p>
                            </div>
                        </body>
                        </html>");

                    _logger.LogInformation("Password reset email sent successfully to {Email}", Input.Email);
                    
                    // Redirect đến trang xác nhận sau khi gửi email thành công
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending password reset email to {Email}", Input.Email);
                    ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi gửi email. Vui lòng thử lại sau.");
                    return Page();
                }
            }

            return Page();
        }
    }
}