using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Dtos
{
    public class ReceiptUploadRequestDto
    {
        public IFormFile File { get; set; } = null!;
        public string? Category { get; set; }
        public string? Notes { get; set; }
    }
}
