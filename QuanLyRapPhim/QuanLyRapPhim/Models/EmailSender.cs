using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace QuanLyRapPhim.Models
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;
            var fromName = _configuration["EmailSettings:FromName"] ?? "CinemaX";

            if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                // Nếu chưa cấu hình SMTP, throw exception để báo lỗi
                throw new InvalidOperationException("Email chưa được cấu hình. Vui lòng kiểm tra cài đặt EmailSettings trong appsettings.json");
            }

            try
            {
                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    client.Timeout = 30000; // 30 seconds timeout

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(fromEmail, fromName);
                        mailMessage.To.Add(email);
                        mailMessage.Subject = subject;
                        mailMessage.Body = message;
                        mailMessage.IsBodyHtml = true;

                        await client.SendMailAsync(mailMessage);
                    }
                }
            }
            catch (SmtpException ex)
            {
                throw new Exception($"Lỗi SMTP khi gửi email: {ex.Message}. Chi tiết: {ex.InnerException?.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi gửi email đến {email}: {ex.Message}", ex);
            }
        }
    }
}
