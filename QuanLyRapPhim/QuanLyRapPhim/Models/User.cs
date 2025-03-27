using Microsoft.AspNetCore.Identity;

namespace QuanLyRapPhim.Models
{
    public class User : IdentityUser
    {
        public string FullName { get; set; }

        public DateOnly DateOfBirth { get; set; }
    }
}
