using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/budget/status")]
    [Authorize]
    public class BudgetStatusController : ControllerBase
    {
        private readonly IBudgetHealthService _budgetHealthService;
        public BudgetStatusController(IBudgetHealthService budgetHealthService)
        {
            _budgetHealthService = budgetHealthService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);
            var snapshot = await _budgetHealthService.GetBudgetHealthAsync(userId, startOfMonth, startOfNextMonth);

            return Ok(new
            {
                budget = snapshot.Budget,
                spent = snapshot.Spent,
                status = snapshot.Status,
                message = snapshot.Message
            });
        }
    }
}
