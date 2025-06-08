using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace QuanLyRapPhim.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(ILogger<ConfirmEmailModel> logger)
        {
            _logger = logger;
        }

        [TempData]
        public string Email { get; set; }
        public string ReturnUrl { get; set; }

        public IActionResult OnGet(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("./Register");
            }

            Email = email;
            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }
    }
}