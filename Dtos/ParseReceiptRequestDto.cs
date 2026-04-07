using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Dtos
{
    public class ParseReceiptRequestDto
    {
        public IFormFile File { get; set; } = null!;
    }
}
