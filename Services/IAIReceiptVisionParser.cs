using Microsoft.AspNetCore.Http;
using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAIReceiptVisionParser
{
    Task<ReceiptParseResult> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
}
