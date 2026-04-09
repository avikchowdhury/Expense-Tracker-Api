namespace ExpenseTracker.Api.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken cancellationToken = default);
    }
}
