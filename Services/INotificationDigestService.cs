using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface INotificationDigestService
    {
        NotificationDeliveryStatusDto GetDeliveryStatus();
        Task<SendTestDigestResultDto> SendTestDigestAsync(int userId, string type, CancellationToken cancellationToken = default);
        Task ProcessDueDigestsAsync(CancellationToken cancellationToken = default);
    }
}
