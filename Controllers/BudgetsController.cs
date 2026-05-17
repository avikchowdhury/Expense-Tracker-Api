using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class BudgetsController : AppControllerBase
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
            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);

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

            var snapshot = await _budgetHealthService.GetBudgetHealthAsync(CurrentUserId, start, end);

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
            var snapshot = await _budgetAdvisorService.GetBudgetAdvisorAsync(CurrentUserId);
            return Ok(snapshot);
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            var budgets = await _unitOfWork.Budgets.Query()
                .Where(x => x.UserId == CurrentUserId)
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
        public async Task<IActionResult> CreateBudget([FromBody] BudgetWriteDto request)
        {
            var budget = new Budget
            {
                UserId = CurrentUserId,
                Category = request.Category,
                MonthlyLimit = request.MonthlyLimit,
                CurrentSpent = 0m,
                LastReset = DateTime.UtcNow
            };

            await _unitOfWork.Budgets.AddAsync(budget);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBudgetById), new { id = budget.Id }, new BudgetDto
            {
                Id = budget.Id,
                UserId = CurrentUserId,
                Category = budget.Category,
                MonthlyLimit = budget.MonthlyLimit,
                CurrentSpent = budget.CurrentSpent,
                LastReset = budget.LastReset
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] BudgetWriteDto request)
        {
            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
            if (budget == null) return NotFound();

            budget.Category = request.Category;
            budget.MonthlyLimit = request.MonthlyLimit;
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
            var budget = await _unitOfWork.Budgets.Query().FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
            if (budget == null) return NotFound();

            _unitOfWork.Budgets.Remove(budget);
            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }
    }
}
