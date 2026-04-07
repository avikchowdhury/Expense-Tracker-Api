using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetNotifications()
        {
            // TODO: Fetch notifications for the user
            var notifications = new List<string> { "Budget exceeded!", "AI suggestion: Review your receipts." };
            return Ok(notifications);
        }

        [HttpPost]
        public IActionResult SendNotification([FromBody] string message)
        {
            // TODO: Send notification to the user
            return Ok();
        }
    }
}
