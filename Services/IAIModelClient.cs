using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAIModelClient
{
    Task<string> GenerateGroundedReplyAsync(
        string userMessage,
        AiInsightSnapshotDto snapshot,
        string fallbackReply,
        CancellationToken cancellationToken = default);
}
