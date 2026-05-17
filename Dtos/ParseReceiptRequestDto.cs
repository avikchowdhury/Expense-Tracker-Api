using Microsoft.AspNetCore.Http;
using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ParseReceiptRequestDto
    {
        [NonEmptyFile(ErrorMessage = ApplicationText.Validation.ReceiptFileRequired)]
        public IFormFile File { get; set; } = null!;
    }
}
