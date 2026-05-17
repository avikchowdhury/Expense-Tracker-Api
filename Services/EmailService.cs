using ExpenseTracker.Shared.Constants;
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
        public string FromName { get; set; } = ApplicationText.Email.FromName;
        public bool EnableSsl { get; set; } = true;
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool IsConfigured()
        {
            var settings = GetSettings();
            return HasRequiredSettings(settings);
        }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(
                toEmail,
                ApplicationText.Email.OtpSubject,
                string.Format(ApplicationText.Email.OtpBodyTemplate, otp),
                cancellationToken: cancellationToken);
        }

        public Task<bool> SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(
                toEmail,
                ApplicationText.Email.PasswordResetSubject,
                string.Format(ApplicationText.Email.PasswordResetBodyTemplate, token),
                cancellationToken: cancellationToken);
        }

        public async Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            bool isHtml = false,
            CancellationToken cancellationToken = default)
        {
            var settings = GetSettings();
            if (!HasRequiredSettings(settings))
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
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            message.To.Add(toEmail);

            using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }

        private EmailSettings GetSettings()
        {
            return _configuration.GetSection(ApplicationText.Configuration.EmailSection).Get<EmailSettings>() ?? new EmailSettings();
        }

        private static bool HasRequiredSettings(EmailSettings settings)
        {
            return !string.IsNullOrWhiteSpace(settings.SmtpHost) &&
                !string.IsNullOrWhiteSpace(settings.FromAddress) &&
                !string.IsNullOrWhiteSpace(settings.Username) &&
                !string.IsNullOrWhiteSpace(settings.Password);
        }
    }
}
