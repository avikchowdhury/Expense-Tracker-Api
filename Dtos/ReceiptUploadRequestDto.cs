using Microsoft.AspNetCore.Http;
using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ReceiptUploadRequestDto
    {
        [NonEmptyFile(ErrorMessage = ApplicationText.Validation.ReceiptFileRequired)]
        public IFormFile File { get; set; } = null!;

        [StringLength(100)]
        public string? Category { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }
    }
}
