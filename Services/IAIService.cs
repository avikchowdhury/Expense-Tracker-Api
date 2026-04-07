using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IAIService
    {
        Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file);
        Task<AiInsightSnapshotDto> GetInsightsAsync(int userId);
        Task<AiChatResponseDto> ChatAsync(int userId, string message);
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
