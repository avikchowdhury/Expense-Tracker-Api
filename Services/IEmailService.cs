namespace ExpenseTracker.Api.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default);
    }
}
