using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class NotificationsController : AppControllerBase
    {
        private readonly IAIService _aiService;
        private readonly INotificationDigestService _notificationDigestService;

        public NotificationsController(
            IAIService aiService,
            INotificationDigestService notificationDigestService)
        {
            _aiService = aiService;
            _notificationDigestService = notificationDigestService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _aiService.GetNotificationsAsync(CurrentUserId);
            return Ok(notifications);
        }

        [HttpGet("email-status")]
        public IActionResult GetEmailStatus()
        {
            return Ok(_notificationDigestService.GetDeliveryStatus());
        }

        [HttpPost("send-test-digest")]
        public async Task<IActionResult> SendTestDigest(
            [FromBody] Dtos.SendTestDigestRequestDto request,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return BadRequest(new { message = "Digest type is required." });
            }

            var result = await _notificationDigestService.SendTestDigestAsync(
                CurrentUserId,
                request.Type,
                cancellationToken);

            return Ok(result);
        }
    }
}
