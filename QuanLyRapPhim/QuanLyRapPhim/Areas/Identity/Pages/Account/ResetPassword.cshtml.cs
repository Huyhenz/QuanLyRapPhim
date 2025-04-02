using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuanLyRapPhim.Models; // Thay thế bằng namespace của bạn

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public ResetPasswordModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public ResetPassword Input { get; set; }

    public void OnGet(string email)
    {
        Input = new ResetPassword { Email = email };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user != null)
            {
                var resetPasswordResult = await _userManager.ResetPasswordAsync(user, await _userManager.GeneratePasswordResetTokenAsync(user), Input.NewPassword);

                if (resetPasswordResult.Succeeded)
                {
                    // Chuyển hướng đến trang thông báo thành công
                    return RedirectToPage("./ResetPasswordConfirmation");
                }

                foreach (var error in resetPasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
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