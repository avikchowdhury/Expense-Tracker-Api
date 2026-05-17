using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ParseReceiptRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
