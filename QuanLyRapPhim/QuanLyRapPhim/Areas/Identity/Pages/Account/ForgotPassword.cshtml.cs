using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuanLyRapPhim.Models; // Thay thế bằng namespace của bạn

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public ForgotPasswordModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public ConfirmEmail Input { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user != null)
            {
                // Chuyển hướng đến trang ResetPassword, truyền email qua
                return RedirectToPage("./ResetPassword", new { email = Input.Email });
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại.");
                return Page();
            }
        }
        return Page();
    }
}