namespace ExpenseTracker.Api.Services
{
    public interface IEmailService
    {
        bool IsConfigured();
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
        Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken cancellationToken = default);
    }
}
