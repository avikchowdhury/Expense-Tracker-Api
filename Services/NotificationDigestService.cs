using System.Collections.Concurrent;
using System.Text;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services
{
    public sealed class NotificationDigestService : INotificationDigestService
    {
        private static readonly ConcurrentDictionary<string, byte> ScheduledDeliveryLocks = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FileStoragePaths _storagePaths;
        private readonly ILogger<NotificationDigestService> _logger;

        public NotificationDigestService(
            IServiceScopeFactory scopeFactory,
            FileStoragePaths storagePaths,
            ILogger<NotificationDigestService> logger)
        {
            _scopeFactory = scopeFactory;
            _storagePaths = storagePaths;
            _logger = logger;
        }

        public NotificationDeliveryStatusDto GetDeliveryStatus()
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            if (emailService.IsConfigured())
            {
                return new NotificationDeliveryStatusDto
                {
                    IsOperational = true,
                    DeliveryMode = ApplicationText.DeliveryModes.Smtp,
                    Message = "Weekly and monthly digests are ready to send through the configured SMTP server."
                };
            }

            return new NotificationDeliveryStatusDto
            {
                IsOperational = true,
                DeliveryMode = ApplicationText.DeliveryModes.FilePreview,
                Message = $"SMTP is not configured yet, so digest previews will be written to {_storagePaths.NotificationPreviewsPath}."
            };
        }

        public async Task<SendTestDigestResultDto> SendTestDigestAsync(
            int userId,
            string type,
            CancellationToken cancellationToken = default)
        {
            var digestType = NormalizeDigestType(type);
            if (digestType == null)
            {
                return new SendTestDigestResultDto
                {
                    Type = type,
                    Delivered = false,
                    Message = ApplicationText.Digests.ChooseDigestType
                };
            }

            using var scope = _scopeFactory.CreateScope();
            var userDigestRepository = scope.ServiceProvider.GetRequiredService<IUserDigestRepository>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var user = await userDigestRepository.GetUserDigestTargetAsync(userId, cancellationToken);

            if (user == null)
            {
                return new SendTestDigestResultDto
                {
                    Type = digestType,
                    Delivered = false,
                    Message = ApplicationText.Digests.UserProfileNotFound
                };
            }

            return await DeliverDigestAsync(
                user,
                digestType,
                aiService,
                emailService,
                isTestSend: true,
                cancellationToken);
        }

        public async Task ProcessDueDigestsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var userDigestRepository = scope.ServiceProvider.GetRequiredService<IUserDigestRepository>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var now = DateTimeOffset.Now;
            var weeklyDay = now.DayOfWeek.ToString();
            var monthlyPeriodKey = $"{now:yyyy-MM}";
            var weeklyPeriodKey = $"{now:yyyy-MM-dd}";

            var users = await userDigestRepository.ListUserDigestTargetsAsync(cancellationToken);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (user.WeeklySummaryEmailEnabled &&
                    string.Equals(user.WeeklySummaryDay, weeklyDay, StringComparison.OrdinalIgnoreCase))
                {
                    await TryDeliverScheduledDigestAsync(
                        user,
                        ApplicationText.Digests.WeeklySummaryType,
                        weeklyPeriodKey,
                        aiService,
                        emailService,
                        cancellationToken);
                }

                if (user.MonthlyReportEmailEnabled && now.Day == 1)
                {
                    await TryDeliverScheduledDigestAsync(
                        user,
                        ApplicationText.Digests.MonthlyReportType,
                        monthlyPeriodKey,
                        aiService,
                        emailService,
                        cancellationToken);
                }
            }
        }

        private async Task TryDeliverScheduledDigestAsync(
            UserDigestTarget user,
            string digestType,
            string periodKey,
            IAIService aiService,
            IEmailService emailService,
            CancellationToken cancellationToken)
        {
            var lockKey = $"{digestType}:{user.Id}:{periodKey}";
            if (!ScheduledDeliveryLocks.TryAdd(lockKey, 0))
            {
                return;
            }

            try
            {
                var result = await DeliverDigestAsync(
                    user,
                    digestType,
                    aiService,
                    emailService,
                    isTestSend: false,
                    cancellationToken);

                if (result.Delivered)
                {
                    _logger.LogInformation(
                        "Delivered {DigestType} digest for user {UserId} via {DeliveryMode}.",
                        digestType,
                        user.Id,
                        result.DeliveryMode);
                    return;
                }

                ScheduledDeliveryLocks.TryRemove(lockKey, out _);
                _logger.LogWarning(
                    "Failed to deliver {DigestType} digest for user {UserId}: {Message}",
                    digestType,
                    user.Id,
                    result.Message);
            }
            catch (Exception ex)
            {
                ScheduledDeliveryLocks.TryRemove(lockKey, out _);
                _logger.LogError(
                    ex,
                    "Error while delivering {DigestType} digest for user {UserId}.",
                    digestType,
                    user.Id);
            }
        }

        private async Task<SendTestDigestResultDto> DeliverDigestAsync(
            UserDigestTarget user,
            string digestType,
            IAIService aiService,
            IEmailService emailService,
            bool isTestSend,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.Now;
            var digest = await BuildDigestAsync(user.Id, digestType, aiService);
            if (digest == null)
            {
                return new SendTestDigestResultDto
                {
                    Type = digestType,
                    Delivered = false,
                    Message = ApplicationText.Digests.UnsupportedDigestType
                };
            }

            if (emailService.IsConfigured())
            {
                var emailed = await emailService.SendEmailAsync(
                    user.Email,
                    digest.Value.Subject,
                    digest.Value.Body,
                    cancellationToken: cancellationToken);

                return new SendTestDigestResultDto
                {
                    Type = digestType,
                    Delivered = emailed,
                    DeliveryMode = ApplicationText.DeliveryModes.Smtp,
                    Message = emailed
                        ? BuildSuccessMessage(digestType, isTestSend, "email")
                        : ApplicationText.Digests.SmtpDeliveryFailed
                };
            }

            var previewPath = await WritePreviewAsync(
                user,
                digestType,
                digest.Value.Subject,
                digest.Value.Body,
                now,
                cancellationToken);

            return new SendTestDigestResultDto
            {
                Type = digestType,
                Delivered = true,
                DeliveryMode = ApplicationText.DeliveryModes.FilePreview,
                Message = $"{BuildSuccessMessage(digestType, isTestSend, "preview")} Saved to {previewPath}.",
                PreviewPath = previewPath
            };
        }

        private async Task<(string Subject, string Body)?> BuildDigestAsync(
            int userId,
            string digestType,
            IAIService aiService)
        {
            return digestType switch
            {
                ApplicationText.Digests.WeeklySummaryType => await BuildWeeklyDigestAsync(userId, aiService),
                ApplicationText.Digests.MonthlyReportType => await BuildMonthlyDigestAsync(userId, aiService),
                _ => null
            };
        }

        private static async Task<(string Subject, string Body)> BuildWeeklyDigestAsync(
            int userId,
            IAIService aiService)
        {
            var summary = await aiService.GetWeeklySummaryAsync(userId);
            var builder = new StringBuilder();
            builder.AppendLine($"Weekly spending digest for {summary.RangeLabel}");
            builder.AppendLine();
            builder.AppendLine($"Total spend: {summary.TotalSpend:C}");
            builder.AppendLine($"Receipts logged: {summary.ReceiptCount}");
            builder.AppendLine($"Top category: {summary.TopCategory}");
            builder.AppendLine($"Forecast risk: {summary.ForecastRisk}");
            builder.AppendLine();
            builder.AppendLine("Recommendation:");
            builder.AppendLine(summary.Recommendation);

            if (summary.TopCategories.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Top categories:");
                foreach (var item in summary.TopCategories)
                {
                    builder.AppendLine($"- {item.Category}: {item.TotalSpend:C}");
                }
            }

            return ($"Weekly spending digest - {summary.RangeLabel}", builder.ToString().Trim());
        }

        private static async Task<(string Subject, string Body)> BuildMonthlyDigestAsync(
            int userId,
            IAIService aiService)
        {
            var summary = await aiService.GetMonthlySummaryAsync(userId);
            var forecast = await aiService.GetSpendingForecastAsync(userId);
            var vendors = await aiService.GetVendorAnalysisAsync(userId);
            var builder = new StringBuilder();
            builder.AppendLine($"Monthly AI report for {summary.Month}");
            builder.AppendLine();
            builder.AppendLine($"Total spend: {summary.TotalSpend:C}");
            builder.AppendLine($"Top category: {summary.TopCategory}");
            builder.AppendLine($"Receipts logged: {summary.ReceiptCount}");
            builder.AppendLine($"Projected month-end: {forecast.ProjectedMonthEnd:C}");
            builder.AppendLine();
            builder.AppendLine("AI summary:");
            builder.AppendLine(summary.AiSummary);
            builder.AppendLine();
            builder.AppendLine("Forecast:");
            builder.AppendLine(forecast.AiNarrative);

            if (summary.Anomalies.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Anomalies:");
                foreach (var anomaly in summary.Anomalies.Take(3))
                {
                    builder.AppendLine($"- {anomaly.Category}: {anomaly.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(vendors.AiObservation))
            {
                builder.AppendLine();
                builder.AppendLine("Vendor insight:");
                builder.AppendLine(vendors.AiObservation);
            }

            return ($"Monthly AI report - {summary.Month}", builder.ToString().Trim());
        }

        private async Task<string> WritePreviewAsync(
            UserDigestTarget user,
            string digestType,
            string subject,
            string body,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var safeEmail = SanitizeForFileName(user.Email);
            var safeType = SanitizeForFileName(digestType);
            var fileName = $"{now:yyyyMMdd-HHmmss}-{user.Id}-{safeType}-{safeEmail}.txt";
            var path = Path.Combine(_storagePaths.NotificationPreviewsPath, fileName);
            var contents = new StringBuilder()
                .AppendLine($"To: {user.Email}")
                .AppendLine($"Subject: {subject}")
                .AppendLine($"Generated: {now:O}")
                .AppendLine()
                .AppendLine(body)
                .ToString();

            await File.WriteAllTextAsync(path, contents, cancellationToken);
            return path;
        }

        private static string? NormalizeDigestType(string? type)
        {
            var normalized = type?.Trim().ToLowerInvariant();
            return normalized switch
            {
                ApplicationText.Digests.WeeklySummaryType => ApplicationText.Digests.WeeklySummaryType,
                ApplicationText.Digests.MonthlyReportType => ApplicationText.Digests.MonthlyReportType,
                _ => null
            };
        }

        private static string BuildSuccessMessage(string digestType, bool isTestSend, string target)
        {
            var label = digestType == ApplicationText.Digests.MonthlyReportType
                ? ApplicationText.Digests.MonthlyReportLabel
                : ApplicationText.Digests.WeeklySummaryLabel;
            return isTestSend
                ? $"Test {label} {target} generated successfully."
                : $"Scheduled {label} {target} generated successfully.";
        }

        private static string SanitizeForFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(invalidChars.Contains(character) ? '-' : character);
            }

            return builder.ToString();
        }
    }
}
