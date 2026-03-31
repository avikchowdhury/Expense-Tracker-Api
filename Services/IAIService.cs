using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Services
{
    public interface IAIService
    {
        Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file);
    }

    public class ReceiptParseResult
    {
        public string Vendor { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; }
        public string Date { get; set; }
        public string RawText { get; set; }
    }
}
