using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using QuanLyRapPhim.Models;
using System.Text.Encodings.Web;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public RegisterModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger,
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
            [DataType(DataType.Text)]
            [Display(Name = "Họ và tên")]
            public string Name { get; set; }

            [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
            [Display(Name = "Ngày sinh")]
            [DataType(DataType.Date)]
            public DateTime DOB { get; set; }

            [Required(ErrorMessage = "Email là bắt buộc.")]
            [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
            [StringLength(100, ErrorMessage = "{0} phải dài ít nhất {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu và mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Tạo mã token duy nhất
                var token = Guid.NewGuid().ToString();
                var cacheKey = $"ConfirmToken_{Input.Email}";

                // Lưu trữ thông tin người dùng trong cache (hết hạn sau 5 phút)
                var cacheEntry = new
                {
                    Input.Name,
                    DOB = Input.DOB,
                    Input.Email,
                    Input.Password,
                    Token = token
                };
                _memoryCache.Set(cacheKey, cacheEntry, TimeSpan.FromMinutes(5));

                // Tạo liên kết xác nhận
                var verificationLink = Url.Page(
                    "/Account/RegisterConfirmation",
                    pageHandler: null,
                    values: new { email = Input.Email, token, returnUrl },
                    protocol: Request.Scheme);

                // Gửi email xác nhận
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(smtpSettings["SenderName"], smtpSettings["SenderEmail"]));
                message.To.Add(new MailboxAddress(Input.Name, Input.Email));
                message.Subject = "Xác nhận đăng ký tài khoản CinemaX";
                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $"<p>Chào {Input.Name},</p>" +
                               $"<p>Vui lòng xác nhận đăng ký tài khoản bằng cách nhấp vào liên kết sau:</p>" +
                               $"<p><a href='{HtmlEncoder.Default.Encode(verificationLink)}'>Xác nhận đăng ký</a></p>" +
                               $"<p>Liên kết này có hiệu lực trong 5 phút (gửi lúc 06:12 PM +07 on Sunday, June 08, 2025).</p>" +
                               $"<p>Trân trọng,<br/>Đội ngũ CinemaX</p>"
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                try
                {
                    await client.ConnectAsync(smtpSettings["Server"], int.Parse(smtpSettings["Port"]), MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(smtpSettings["Username"], smtpSettings["Password"]);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                    _logger.LogInformation("Verification email sent to {Email} at {Time}", Input.Email, DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send verification email to {Email}", Input.Email);
                    ModelState.AddModelError(string.Empty, "Không thể gửi email xác nhận. Vui lòng thử lại sau.");
                    return Page();
                }

                // Chuyển hướng đến trang ConfirmEmail
                return RedirectToPage("./ConfirmEmail", new { email = Input.Email, returnUrl });
            }

            return Page();
        }
    }
}