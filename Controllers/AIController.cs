using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class AIController : AppControllerBase
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
            var result = await _aiService.GetInsightsAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            var result = await _aiService.GetSubscriptionsAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Please send a question for the assistant." });
            }

            var result = await _aiService.ChatAsync(CurrentUserId, request.Message);
            return Ok(result);
        }

        [HttpGet("spending-anomalies")]
        public async Task<IActionResult> GetSpendingAnomalies()
        {
            var result = await _aiService.GetSpendingAnomaliesAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("monthly-summary")]
        public async Task<IActionResult> GetMonthlySummary()
        {
            var result = await _aiService.GetMonthlySummaryAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast()
        {
            var result = await _aiService.GetSpendingForecastAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpPost("forecast/what-if")]
        public async Task<IActionResult> GetWhatIfForecast([FromBody] WhatIfForecastRequestDto request)
        {
            var result = await _aiService.GetWhatIfForecastAsync(CurrentUserId, request ?? new WhatIfForecastRequestDto());
            return Ok(result);
        }

        [HttpGet("weekly-summary")]
        public async Task<IActionResult> GetWeeklySummary()
        {
            var result = await _aiService.GetWeeklySummaryAsync(CurrentUserId);
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
            var result = await _aiService.GetVendorAnalysisAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpPost("check-duplicate")]
        public async Task<IActionResult> CheckDuplicate([FromBody] Dtos.DuplicateCheckRequestDto request)
        {
            var result = await _aiService.CheckDuplicateReceiptAsync(CurrentUserId, request.Vendor, request.Amount, request.Date);
            return Ok(result);
        }
    }
}
