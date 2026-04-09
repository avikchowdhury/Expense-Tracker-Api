using System.Net;
using System.Net.Mail;

namespace ExpenseTracker.Api.Services
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "AI Expense Tracker";
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
        {
            var settings = _configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
            if (string.IsNullOrWhiteSpace(settings.SmtpHost) ||
                string.IsNullOrWhiteSpace(settings.FromAddress) ||
                string.IsNullOrWhiteSpace(settings.Username) ||
                string.IsNullOrWhiteSpace(settings.Password))
            {
                return false;
            }

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl,
                Credentials = new NetworkCredential(settings.Username, settings.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName),
                Subject = "Your AI Expense Tracker OTP",
                Body = $"Your OTP is {otp}. It will expire in 10 minutes.",
                IsBodyHtml = false
            };

            message.To.Add(toEmail);

            using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken cancellationToken = default)
        {
            var settings = _configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
            if (string.IsNullOrWhiteSpace(settings.SmtpHost) ||
                string.IsNullOrWhiteSpace(settings.FromAddress) ||
                string.IsNullOrWhiteSpace(settings.Username) ||
                string.IsNullOrWhiteSpace(settings.Password))
            {
                return false;
            }

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl,
                Credentials = new NetworkCredential(settings.Username, settings.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName),
                Subject = "Password Reset — AI Expense Tracker",
                Body = $"Your password reset code is: {token}\n\nIt will expire in 15 minutes.\n\nIf you did not request this, you can safely ignore this email.",
                IsBodyHtml = false
            };

            message.To.Add(toEmail);

            using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
    }
}
