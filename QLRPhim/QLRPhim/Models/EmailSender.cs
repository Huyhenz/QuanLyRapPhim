using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace QLRPhim.Models
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string message)
        {
            // Logic gửi email (ví dụ: sử dụng SMTP hoặc SendGrid)
            return Task.CompletedTask;
        }
    }
}
