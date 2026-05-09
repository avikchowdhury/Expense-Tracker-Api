namespace ExpenseTracker.Api.Services
{
    public sealed class NotificationDigestBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(15);

        private readonly INotificationDigestService _notificationDigestService;
        private readonly ILogger<NotificationDigestBackgroundService> _logger;

        public NotificationDigestBackgroundService(
            INotificationDigestService notificationDigestService,
            ILogger<NotificationDigestBackgroundService> logger)
        {
            _notificationDigestService = notificationDigestService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunOnceSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(RunInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceSafelyAsync(stoppingToken);
            }
        }

        private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _notificationDigestService.ProcessDueDigestsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification digest cycle failed.");
            }
        }
    }
}
