using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlySpendings([FromQuery] int months = 6)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var result = await _analyticsService.GetMonthlySpendingAsync(userId, months);
            return Ok(result);
        }

        [HttpGet("category")]
        public async Task<IActionResult> GetCategoryBreakdown()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var result = await _analyticsService.GetCategoryBreakdownAsync(userId);
            return Ok(result.Select(x => new { Category = x.Category, Total = x.Total }));
        }
    }
}
