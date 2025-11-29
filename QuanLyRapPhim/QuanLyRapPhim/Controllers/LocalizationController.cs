using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyRapPhim.Controllers
{
    public class LocalizationController : Controller
    {
        public IActionResult SetLanguage(string culture, string returnUrl = "/")
        {
            // Validate culture
            if (culture != "vi" && culture != "en")
            {
                culture = "vi"; // Default to Vietnamese
            }

            // Set cookie với các options đầy đủ
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/",
                    HttpOnly = false,
                    IsEssential = true, // QUAN TRỌNG: Cookie này là bắt buộc
                    SameSite = SameSiteMode.Lax // Cho phép cookie hoạt động với redirect
                });

            // Đảm bảo returnUrl an toàn
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }
    }
}