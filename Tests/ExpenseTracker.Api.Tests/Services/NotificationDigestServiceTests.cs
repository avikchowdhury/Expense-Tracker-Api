using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Api.Tests.Services;

public sealed class NotificationDigestServiceTests
{
    [Fact]
    public async Task SendTestDigestAsync_WithoutSmtp_WritesPreviewFile()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            await using var provider = BuildProvider(tempRoot, ConfigureSingleUserForToday);
            var service = provider.GetRequiredService<INotificationDigestService>();

            var result = await service.SendTestDigestAsync(1, "weekly-summary");

            Assert.True(result.Delivered, result.Message);
            Assert.Equal("file-preview", result.DeliveryMode);
            Assert.NotNull(result.PreviewPath);
            Assert.True(File.Exists(result.PreviewPath!));
            Assert.Contains("Weekly spending digest", await File.ReadAllTextAsync(result.PreviewPath!));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task ProcessDueDigestsAsync_DoesNotDuplicateSameWeeklyDigestInSameProcess()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            await using var provider = BuildProvider(tempRoot, ConfigureSingleUserForToday);
            var service = provider.GetRequiredService<INotificationDigestService>();
            var previewsPath = provider.GetRequiredService<FileStoragePaths>().NotificationPreviewsPath;

            await service.ProcessDueDigestsAsync();
            await service.ProcessDueDigestsAsync();

            var files = Directory.GetFiles(previewsPath, "*.txt");
            Assert.Single(files);
            Assert.Contains("weekly-summary", Path.GetFileName(files[0]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static ServiceProvider BuildProvider(string tempRoot, Action<ExpenseTrackerDbContext> seed)
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ExpenseTrackerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped<IAIService, FakeAiService>();
        services.AddScoped<IEmailService, FakeEmailService>();
        services.AddSingleton(new FileStoragePaths
        {
            RootPath = tempRoot,
            AvatarsPath = Path.Combine(tempRoot, "avatars"),
            ReceiptsPath = Path.Combine(tempRoot, "receipts"),
            NotificationPreviewsPath = Path.Combine(tempRoot, "notification-previews"),
        });
        services.AddSingleton<INotificationDigestService, NotificationDigestService>();

        var provider = services.BuildServiceProvider();

        Directory.CreateDirectory(Path.Combine(tempRoot, "avatars"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "receipts"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "notification-previews"));

        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExpenseTrackerDbContext>();
        seed(dbContext);
        dbContext.SaveChanges();

        return provider;
    }

    private static void ConfigureSingleUserForToday(ExpenseTrackerDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = 1,
            Email = "digest-user@example.com",
            PasswordHash = "hash",
            WeeklySummaryEmailEnabled = true,
            MonthlyReportEmailEnabled = false,
            WeeklySummaryDay = DateTimeOffset.Now.DayOfWeek.ToString(),
        });
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "expense-tracker-digests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool IsConfigured() => false;

        public Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeAiService : IAIService
    {
        public Task<ReceiptParseResult> ParseReceiptAsync(Microsoft.AspNetCore.Http.IFormFile file) => throw new NotImplementedException();
        public Task<AiInsightSnapshotDto> GetInsightsAsync(int userId) => throw new NotImplementedException();
        public Task<List<AiSubscriptionInsightDto>> GetSubscriptionsAsync(int userId) => throw new NotImplementedException();
        public Task<AiChatResponseDto> ChatAsync(int userId, string message) => throw new NotImplementedException();
        public Task<List<SpendingAnomalyDto>> GetSpendingAnomaliesAsync(int userId) => throw new NotImplementedException();

        public Task<MonthlySummaryDto> GetMonthlySummaryAsync(int userId) => Task.FromResult(new MonthlySummaryDto
        {
            Month = "May 2026",
            TotalSpend = 920m,
            TopCategory = "Food",
            ReceiptCount = 12,
            AiSummary = "Food remained the largest expense category.",
            Anomalies =
            [
                new SpendingAnomalyDto
                {
                    Category = "Food",
                    Message = "Food spending rose faster than usual."
                }
            ]
        });

        public Task<SpendingForecastDto> GetSpendingForecastAsync(int userId) => Task.FromResult(new SpendingForecastDto
        {
            CurrentSpend = 920m,
            ProjectedMonthEnd = 1380m,
            DailyAverage = 46m,
            DaysElapsed = 20,
            DaysRemaining = 10,
            Trend = "warning",
            AiNarrative = "You are pacing slightly above the ideal curve.",
            BudgetAmount = 1500m,
            TopCategory = "Food"
        });

        public Task<WhatIfForecastDto> GetWhatIfForecastAsync(int userId, WhatIfForecastRequestDto request) => throw new NotImplementedException();

        public Task<WeeklySummaryDto> GetWeeklySummaryAsync(int userId) => Task.FromResult(new WeeklySummaryDto
        {
            RangeLabel = "May 3 - May 9",
            TotalSpend = 320m,
            ReceiptCount = 5,
            TopCategory = "Groceries",
            ForecastRisk = "Warning",
            Recommendation = "Slow down discretionary spending over the weekend.",
            TopCategories =
            [
                new WeeklyCategorySpendDto
                {
                    Category = "Groceries",
                    TotalSpend = 180m
                }
            ]
        });

        public Task<List<NotificationDto>> GetNotificationsAsync(int userId) => throw new NotImplementedException();
        public Task<ParseTextResultDto> ParseTextExpenseAsync(string text) => throw new NotImplementedException();

        public Task<VendorAnalysisDto> GetVendorAnalysisAsync(int userId) => Task.FromResult(new VendorAnalysisDto
        {
            Month = "May 2026",
            AiObservation = "Your top vendors are still concentrated around groceries and dining."
        });

        public Task<DuplicateCheckResultDto> CheckDuplicateReceiptAsync(int userId, string vendor, decimal amount, string date) => throw new NotImplementedException();
    }
}
