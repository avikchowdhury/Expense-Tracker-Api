using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BudgetsController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public BudgetsController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBudgetById(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budget = await _dbContext.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (budget == null) return NotFound();

            return Ok(new BudgetDto
            {
                Id = budget.Id,
                UserId = budget.UserId,
                Category = budget.Category,
                MonthlyLimit = budget.MonthlyLimit,
                CurrentSpent = budget.CurrentSpent,
                LastReset = budget.LastReset
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetBudgetStatus([FromQuery] string? period)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            DateTime start, end;

            if (!string.IsNullOrEmpty(period) && DateTime.TryParse(period + "-01", out var parsed))
            {
                start = new DateTime(parsed.Year, parsed.Month, 1);
                end = start.AddMonths(1);
            }
            else
            {
                var now = DateTime.UtcNow;
                start = new DateTime(now.Year, now.Month, 1);
                end = start.AddMonths(1);
            }

            var budget = await _dbContext.Budgets
                .Where(b => b.UserId == userId && b.Category == "General")
                .OrderByDescending(b => b.LastReset)
                .FirstOrDefaultAsync();

            if (budget == null)
            {
                return Ok(new { budget = 0.0, spent = 0.0, status = "ok", message = "No budget set." });
            }

            var spent = await _dbContext.Expenses
                .Where(e => e.UserId == userId && e.Date >= start && e.Date < end)
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
                message = "You are close to your budget limit.";
            }
            else
            {
                status = "over";
                message = "You have exceeded your budget!";
            }

            return Ok(new { budget = budget.MonthlyLimit, spent, status, message });
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budgets = await _dbContext.Budgets
                .Where(x => x.UserId == userId)
                .ToListAsync();

            return Ok(budgets.Select(x => new BudgetDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Category = x.Category,
                MonthlyLimit = x.MonthlyLimit,
                CurrentSpent = x.CurrentSpent,
                LastReset = x.LastReset
            }));
        }

        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] BudgetDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budget = new Budget
            {
                UserId = userId,
                Category = request.Category,
                MonthlyLimit = request.MonthlyLimit,
                CurrentSpent = 0m,
                LastReset = DateTime.UtcNow
            };

            _dbContext.Budgets.Add(budget);
            await _dbContext.SaveChangesAsync();

            request.Id = budget.Id;
            request.UserId = userId;
            request.CurrentSpent = budget.CurrentSpent;
            request.LastReset = budget.LastReset;

            return CreatedAtAction(nameof(GetBudgets), new { id = budget.Id }, request);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] BudgetDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budget = await _dbContext.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null) return NotFound();

            budget.Category = request.Category;
            budget.MonthlyLimit = request.MonthlyLimit;
            budget.CurrentSpent = request.CurrentSpent;
            // Always set LastReset to current UTC time on update
            budget.LastReset = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new BudgetDto
            {
                Id = budget.Id,
                UserId = budget.UserId,
                Category = budget.Category,
                MonthlyLimit = budget.MonthlyLimit,
                CurrentSpent = budget.CurrentSpent,
                LastReset = budget.LastReset
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budget = await _dbContext.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null) return NotFound();

            _dbContext.Budgets.Remove(budget);
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }
    }
}