using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBudgetHealthService _budgetHealthService;
        private readonly IBudgetAdvisorService _budgetAdvisorService;

        public BudgetsController(
            IUnitOfWork unitOfWork,
            IBudgetHealthService budgetHealthService,
            IBudgetAdvisorService budgetAdvisorService)
        {
            _unitOfWork = unitOfWork;
            _budgetHealthService = budgetHealthService;
            _budgetAdvisorService = budgetAdvisorService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBudgetById(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

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

            var snapshot = await _budgetHealthService.GetBudgetHealthAsync(userId, start, end);

            return Ok(new
            {
                budget = snapshot.Budget,
                spent = snapshot.Spent,
                status = snapshot.Status,
                message = snapshot.Message
            });
        }

        [HttpGet("advisor")]
        public async Task<IActionResult> GetBudgetAdvisor()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var snapshot = await _budgetAdvisorService.GetBudgetAdvisorAsync(userId);
            return Ok(snapshot);
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var budgets = await _unitOfWork.Budgets.Query()
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

            await _unitOfWork.Budgets.AddAsync(budget);
            await _unitOfWork.SaveChangesAsync();

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

            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null) return NotFound();

            budget.Category = request.Category;
            budget.MonthlyLimit = request.MonthlyLimit;
            budget.CurrentSpent = request.CurrentSpent;
            // Always set LastReset to current UTC time on update
            budget.LastReset = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

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

            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null) return NotFound();

            _unitOfWork.Budgets.Remove(budget);
            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }
    }
}
