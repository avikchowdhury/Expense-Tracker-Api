using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IAIService
    {
        Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file);
        Task<AiInsightSnapshotDto> GetInsightsAsync(int userId);
        Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId);
        Task<AiChatResponseDto> ChatAsync(int userId, string message);
        Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId);
        Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId);
        Task<SpendingForecastDto> GetSpendingForecastAsync(int userId);
        Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request);
        Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId);
        Task<List<NotificationDto>> GetNotificationsAsync(int userId);
        Task<ParseTextResultDto> ParseTextExpenseAsync(string text);
        Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId);
        Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date);
    }

    public class ReceiptParseResult
    {
        public string Vendor { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
    }
}
