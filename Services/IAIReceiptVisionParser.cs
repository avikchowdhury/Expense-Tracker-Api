using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Services;

public interface IAIReceiptVisionParser
{
    Task<ReceiptParseResult> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
}
