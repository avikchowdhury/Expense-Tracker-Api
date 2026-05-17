using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public sealed class AIService : IAIService
{
    private readonly IAIReceiptVisionParser _receiptVisionParser;
    private readonly IAIExpenseTextParser _expenseTextParser;
    private readonly IAIInsightsService _aiInsightsService;
    private readonly IAISpendingAnalysisService _spendingAnalysisService;
    private readonly IAINotificationService _notificationService;

    public AIService(
        IAIReceiptVisionParser receiptVisionParser,
        IAIExpenseTextParser expenseTextParser,
        IAIInsightsService aiInsightsService,
        IAISpendingAnalysisService spendingAnalysisService,
        IAINotificationService notificationService)
    {
        _receiptVisionParser = receiptVisionParser;
        _expenseTextParser = expenseTextParser;
        _aiInsightsService = aiInsightsService;
        _spendingAnalysisService = spendingAnalysisService;
        _notificationService = notificationService;
    }

    public Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file)
    {
        return _receiptVisionParser.ParseAsync(file);
    }

    public Task<AiInsightSnapshotDto> GetInsightsAsync(int userId)
    {
        return _aiInsightsService.GetInsightsAsync(userId);
    }

    public Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId)
    {
        return _aiInsightsService.GetSubscriptionsAsync(userId);
    }

    public Task<AiChatResponseDto> ChatAsync(int userId, string message)
    {
        return _aiInsightsService.ChatAsync(userId, message);
    }

    public Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId)
    {
        return _spendingAnalysisService.GetSpendingAnomaliesAsync(userId);
    }

    public Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId)
    {
        return _spendingAnalysisService.GetMonthlySummaryAsync(userId);
    }

    public Task<SpendingForecastDto> GetSpendingForecastAsync(int userId)
    {
        return _spendingAnalysisService.GetSpendingForecastAsync(userId);
    }

    public Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request)
    {
        return _spendingAnalysisService.GetWhatIfForecastAsync(userId, request);
    }

    public Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId)
    {
        return _spendingAnalysisService.GetWeeklySummaryAsync(userId);
    }

    public Task<List<NotificationDto>> GetNotificationsAsync(int userId)
    {
        return _notificationService.GetNotificationsAsync(userId);
    }

    public Task<ParseTextResultDto> ParseTextExpenseAsync(string text)
    {
        return _expenseTextParser.ParseAsync(text);
    }

    public Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId)
    {
        return _spendingAnalysisService.GetVendorAnalysisAsync(userId);
    }

    public Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date)
    {
        return _spendingAnalysisService.CheckDuplicateReceiptAsync(userId, vendor, amount, date);
    }
}
