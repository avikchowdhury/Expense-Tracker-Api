using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class AnalyticsController : AppControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlySpendings([FromQuery] int months = 6)
        {
            var result = await _analyticsService.GetMonthlySpendingAsync(CurrentUserId, months);
            return Ok(result);
        }

        [HttpGet("category")]
        public async Task<IActionResult> GetCategoryBreakdown()
        {
            var result = await _analyticsService.GetCategoryBreakdownAsync(CurrentUserId);
            return Ok(result.Select(x => new { Category = x.Category, Total = x.Total }));
        }
    }
}
