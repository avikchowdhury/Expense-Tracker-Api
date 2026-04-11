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

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetSpendingForecastAsync(userId);
            return Ok(result);
        }

        [HttpPost("forecast/what-if")]
        public async Task<IActionResult> GetWhatIfForecast([FromBody] WhatIfForecastRequestDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetWhatIfForecastAsync(userId, request ?? new WhatIfForecastRequestDto());
            return Ok(result);
        }

        [HttpGet("weekly-summary")]
        public async Task<IActionResult> GetWeeklySummary()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetWeeklySummaryAsync(userId);
            return Ok(result);
        }

        [HttpPost("parse-text")]
        public async Task<IActionResult> ParseText([FromBody] Dtos.ParseTextRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { message = "Text is required." });

            var result = await _aiService.ParseTextExpenseAsync(request.Text);
            return Ok(result);
        }

        [HttpGet("vendor-analysis")]
        public async Task<IActionResult> GetVendorAnalysis()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.GetVendorAnalysisAsync(userId);
            return Ok(result);
        }

        [HttpPost("check-duplicate")]
        public async Task<IActionResult> CheckDuplicate([FromBody] Dtos.DuplicateCheckRequestDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _aiService.CheckDuplicateReceiptAsync(userId, request.Vendor, request.Amount, request.Date);
            return Ok(result);
        }
    }
}
