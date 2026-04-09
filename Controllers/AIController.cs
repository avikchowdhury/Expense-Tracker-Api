using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;

        public AIController(IAIService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("parse-receipt")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseReceipt([FromForm] Dtos.ParseReceiptRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("Please provide a receipt file.");
            }

            var result = await _aiService.ParseReceiptAsync(request.File);
            return Ok(result);
        }

        [HttpGet("insights")]
        public async Task<IActionResult> GetInsights()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var result = await _aiService.GetInsightsAsync(userId);
            return Ok(result);
        }

        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var result = await _aiService.GetSubscriptionsAsync(userId);
            return Ok(result);
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Please send a question for the assistant." });
            }

            var result = await _aiService.ChatAsync(userId, request.Message);
            return Ok(result);
        }

        [HttpGet("spending-anomalies")]
        public async Task<IActionResult> GetSpendingAnomalies()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetSpendingAnomaliesAsync(userId);
            return Ok(result);
        }

        [HttpGet("monthly-summary")]
        public async Task<IActionResult> GetMonthlySummary()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetMonthlySummaryAsync(userId);
            return Ok(result);
        }
    }
}
