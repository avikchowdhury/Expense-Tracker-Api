using System.Security.Claims;
using ExpenseTracker.Api.Controllers;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Tests.Controllers;

public class AIControllerTests
{
    [Fact]
    public async Task GetForecast_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
    {
        var service = new FakeAiService();
        var controller = CreateController(service);

        var result = await controller.GetForecast();

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(service.LastForecastUserId);
    }

    [Fact]
    public async Task GetForecast_ReturnsOk_WhenUserIdClaimIsPresent()
    {
        var forecast = new SpendingForecastDto
        {
            CurrentSpend = 250m,
            ProjectedMonthEnd = 500m,
            DailyAverage = 25m,
            DaysElapsed = 10,
            DaysRemaining = 20,
            Trend = "warning",
            AiNarrative = "Watch spending."
        };

        var service = new FakeAiService { ForecastToReturn = forecast };
        var controller = CreateController(service, userId: 42);

        var result = await controller.GetForecast();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<SpendingForecastDto>(ok.Value);
        Assert.Same(forecast, payload);
        Assert.Equal(42, service.LastForecastUserId);
    }

    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenMessageIsBlank()
    {
        var service = new FakeAiService();
        var controller = CreateController(service, userId: 42);

        var result = await controller.Chat(new AiChatRequestDto { Message = "   " });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(service.ChatCalled);
    }

    private static AIController CreateController(FakeAiService service, int? userId = null)
    {
        var controller = new AIController(service);
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            }, "TestAuth")
            : new ClaimsIdentity();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    private sealed class FakeAiService : IAIService
    {
        public SpendingForecastDto ForecastToReturn { get; set; } = new();
        public int? LastForecastUserId { get; private set; }
        public bool ChatCalled { get; private set; }

        public Task<ReceiptParseResult> ParseReceiptAsync(IFormFile file) => throw new NotImplementedException();
        public Task<AiInsightSnapshotDto> GetInsightsAsync(int userId) => throw new NotImplementedException();
        public Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId) => throw new NotImplementedException();

        public Task<AiChatResponseDto> ChatAsync(int userId, string message)
        {
            ChatCalled = true;
            return Task.FromResult(new AiChatResponseDto());
        }

        public Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId) => throw new NotImplementedException();
        public Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId) => throw new NotImplementedException();
        public Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request) => throw new NotImplementedException();
        public Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId) => throw new NotImplementedException();

        public Task<SpendingForecastDto> GetSpendingForecastAsync(int userId)
        {
            LastForecastUserId = userId;
            return Task.FromResult(ForecastToReturn);
        }

        public Task<List<NotificationDto>> GetNotificationsAsync(int userId) => throw new NotImplementedException();
        public Task<ParseTextResultDto> ParseTextExpenseAsync(string text) => throw new NotImplementedException();
        public Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId) => throw new NotImplementedException();
        public Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date) => throw new NotImplementedException();
    }
}
