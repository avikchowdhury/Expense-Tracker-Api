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

        public NotificationsController(IAIService aiService)
        {
            _aiService = aiService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _aiService.GetNotificationsAsync(CurrentUserId);
            return Ok(notifications);
        }
    }
}
