using ExpenseTracker.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/budget/status")]
    [Authorize]
    public class BudgetStatusController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        public BudgetStatusController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var budget = await _dbContext.Budgets
                .Where(b => b.UserId == userId && b.Category == "General")
                .OrderByDescending(b => b.LastReset)
                .FirstOrDefaultAsync();

            if (budget == null)
            {
                return Ok(new { budget = 0.0, spent = 0.0, status = "ok", message = "No budget set." });
            }

            var spent = await _dbContext.Expenses
                .Where(e => e.UserId == userId && e.Date >= startOfMonth)
                .SumAsync(e => (decimal?)e.Amount) ?? 0.0m;

            string status;
            string message;
            if (spent < budget.MonthlyLimit * 0.8m)
            {
                status = "ok";
                message = "You are well within your budget.";
            }
            else if (spent < budget.MonthlyLimit)
            {
                status = "warning";
                message = "You are close to exceeding your budget!";
            }
            else
            {
                status = "over";
                message = "You have exceeded your budget!";
            }

            return Ok(new
            {
                budget = budget.MonthlyLimit,
                spent,
                status,
                message
            });
        }
    }
}
