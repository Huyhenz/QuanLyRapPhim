using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QuanLyRapPhim.Models;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RegisterConfirmationModel> _logger;

        public RegisterConfirmationModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IUserStore<User> userStore,
            IMemoryCache memoryCache,
            ILogger<RegisterConfirmationModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public string Email { get; set; }
        public string ReturnUrl { get; set; }

        public IActionResult OnGet(string email, string token, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToPage("./Register");
            }

            Email = email;
            ReturnUrl = returnUrl ?? Url.Content("~/");

            var cacheKey = $"ConfirmToken_{email}";
            if (!_memoryCache.TryGetValue(cacheKey, out var cachedData))
            {
                ModelState.AddModelError(string.Empty, "Liên kết xác nhận đã hết hạn hoặc không hợp lệ. Vui lòng đăng ký lại.");
                return Page();
            }

            var cached = (dynamic)cachedData;
            if (cached.Token != token)
            {
                ModelState.AddModelError(string.Empty, "Liên kết xác nhận không hợp lệ.");
                return Page();
            }

            // Tạo tài khoản nếu token hợp lệ (không đăng nhập tự động)
            var user = Activator.CreateInstance<User>();
            user.FullName = cached.Name;
            user.DateOfBirth = DateOnly.FromDateTime(cached.DOB);

            _userStore.SetUserNameAsync(user, cached.Email, CancellationToken.None).GetAwaiter().GetResult();
            _emailStore.SetEmailAsync(user, cached.Email, CancellationToken.None).GetAwaiter().GetResult();
            var result = _userManager.CreateAsync(user, cached.Password).GetAwaiter().GetResult();

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password for {Email}", (string)cached.Email);
                _userManager.AddToRoleAsync(user, "User").GetAwaiter().GetResult(); // Gán quyền mặc định

                // Xóa cache sau khi đăng ký thành công
                _memoryCache.Remove(cacheKey);

                return Page();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
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