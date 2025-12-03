using Microsoft.AspNetCore.Identity;

namespace QuanLyRapPhim.Models
{
    public class User : IdentityUser
    {
        public string FullName { get; set; }

        public DateOnly DateOfBirth { get; set; }

        public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();
    }
}
