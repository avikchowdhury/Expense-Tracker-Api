using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAIInsightsService
{
    Task<AiInsightSnapshotDto> GetInsightsAsync(int userId);
    Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId);
    Task<AiChatResponseDto> ChatAsync(int userId, string message);
}
