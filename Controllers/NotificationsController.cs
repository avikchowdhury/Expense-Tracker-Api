using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IAIService _aiService;

        public NotificationsController(IAIService aiService)
        {
            _aiService = aiService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var notifications = await _aiService.GetNotificationsAsync(userId);
            return Ok(notifications);
        }
    }
}
