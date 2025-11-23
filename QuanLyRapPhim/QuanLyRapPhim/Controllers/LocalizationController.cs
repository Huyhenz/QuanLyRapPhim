using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

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

            // Set cookie
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            // Redirect back to returnUrl or home
            return LocalRedirect(returnUrl ?? "/");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}