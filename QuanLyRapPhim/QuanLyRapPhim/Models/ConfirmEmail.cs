using System.ComponentModel.DataAnnotations;

namespace QuanLyRapPhim.Models

{
    public class ConfirmEmail
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
