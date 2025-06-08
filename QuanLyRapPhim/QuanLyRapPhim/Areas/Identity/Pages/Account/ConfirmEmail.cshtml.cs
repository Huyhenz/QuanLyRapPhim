using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly ILogger<ConfirmEmailModel> _logger;
        private readonly UserManager<User> _userManager;

        public ConfirmEmailModel(ILogger<ConfirmEmailModel> logger, UserManager<User> userManager)
        {
            _logger = logger;
            _userManager = userManager;
        }

        [TempData]
        public string Email { get; set; }
        public string ReturnUrl { get; set; }
        public string ErrorMessage { get; set; }

        public IActionResult OnGet(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("./Register");
            }

            // Kiểm tra xem email đã tồn tại trong database chưa
            var existingUser = _userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
            if (existingUser != null)
            {
                ErrorMessage = "Gmail này đã được đăng ký rồi. Vui lòng sử dụng email khác hoặc đăng nhập.";
                return Page();
            }

            Email = email;
            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }
    }
}