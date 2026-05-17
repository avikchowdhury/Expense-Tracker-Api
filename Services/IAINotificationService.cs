using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAINotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(int userId);
}
