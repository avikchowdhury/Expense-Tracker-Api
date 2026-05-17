using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAIExpenseTextParser
{
    Task<ParseTextResultDto> ParseAsync(string text, CancellationToken cancellationToken = default);
}
