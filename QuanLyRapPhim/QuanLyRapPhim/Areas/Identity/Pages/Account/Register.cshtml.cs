// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Data;
using Microsoft.EntityFrameworkCore;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly DBContext _context;

        public RegisterModel(
            UserManager<User> userManager,
            IUserStore<User> userStore,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            DBContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
                [Required]
                [DataType(DataType.Text)]
                [Display(Name = "Full name")]
                public string Name { get; set; }

                [Required]
                [Display(Name = "Birth Date")]
                [DataType(DataType.Date)]
                public DateTime DOB { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(InputModel input, string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                // Kiểm tra email đã tồn tại chưa (trong User hoặc PendingUser)
                var existingUser = await _userManager.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "Email này đã được sử dụng.");
                    return Page();
                }

                var existingPendingUser = await _context.PendingUsers
                    .FirstOrDefaultAsync(p => p.Email == Input.Email);
                if (existingPendingUser != null)
                {
                    // Xóa pending user cũ nếu có
                    _context.PendingUsers.Remove(existingPendingUser);
                    await _context.SaveChangesAsync();
                }

                // Tạo user tạm để hash password
                var tempUser = CreateUser();
                await _userStore.SetUserNameAsync(tempUser, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(tempUser, Input.Email, CancellationToken.None);
                
                // Hash password
                var passwordHasher = _userManager.PasswordHasher;
                var hashedPassword = passwordHasher.HashPassword(tempUser, Input.Password);

                // Tạo token xác nhận
                var confirmationToken = Guid.NewGuid().ToString("N");

                // Lưu vào PendingUser (chưa lưu vào DB Users)
                var pendingUser = new PendingUser
                {
                    FullName = Input.Name,
                    DateOfBirth = DateOnly.FromDateTime(Input.DOB),
                    Email = Input.Email,
                    PasswordHash = hashedPassword,
                    ConfirmationToken = confirmationToken,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddDays(1) // Token hết hạn sau 24 giờ
                };

                _context.PendingUsers.Add(pendingUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pending user created, waiting for email confirmation.");

                // Tạo link xác nhận
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", token = confirmationToken, returnUrl = returnUrl },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(Input.Email, "Xác nhận email đăng ký - CinemaX",
                    $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                        <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 10px; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                            <h2 style='color: #e50914; text-align: center;'>Chào mừng đến với CinemaX!</h2>
                            <p style='color: #333; font-size: 16px;'>Xin chào <strong>{Input.Name}</strong>,</p>
                            <p style='color: #333; font-size: 16px;'>Cảm ơn bạn đã đăng ký tài khoản tại CinemaX. Để hoàn tất đăng ký, vui lòng xác nhận email của bạn bằng cách nhấp vào nút bên dưới:</p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #e50914; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>Xác nhận email</a>
                            </div>
                            <p style='color: #666; font-size: 14px;'>Hoặc copy và dán link sau vào trình duyệt:</p>
                            <p style='color: #666; font-size: 12px; word-break: break-all;'>{HtmlEncoder.Default.Encode(callbackUrl)}</p>
                            <p style='color: #666; font-size: 14px; margin-top: 30px;'>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                            <p style='color: #ff9800; font-size: 12px; margin-top: 20px;'><strong>Lưu ý:</strong> Link xác nhận sẽ hết hạn sau 24 giờ.</p>
                            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                            <p style='color: #999; font-size: 12px; text-align: center;'>© 2024 CinemaX. All rights reserved.</p>
                        </div>
                    </body>
                    </html>");

                // Luôn redirect đến trang xác nhận
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'. " +
                    $"Ensure that '{nameof(User)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<User> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<User>)_userStore;
        }
    }
}
