using Microsoft.AspNetCore.Identity;
using Microsoft.VisualBasic;

namespace QLRPhim.Models
{
    public class User : IdentityUser
    {
        public string Fullname { get; set; }

        public DateOnly DateOfBirth { get; set; }
    }
}
