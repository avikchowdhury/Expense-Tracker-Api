using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAISpendingAnalysisService
{
    Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId);
    Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId);
    Task<SpendingForecastDto> GetSpendingForecastAsync(int userId);
    Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request);
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId);
    Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId);
    Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date);
}
