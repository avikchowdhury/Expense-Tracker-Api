using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/budget/status")]
    [AppAuthorize]
    public class BudgetStatusController : AppControllerBase
    {
        private readonly IBudgetHealthService _budgetHealthService;
        public BudgetStatusController(IBudgetHealthService budgetHealthService)
        {
            _budgetHealthService = budgetHealthService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);
            var snapshot = await _budgetHealthService.GetBudgetHealthAsync(CurrentUserId, startOfMonth, startOfNextMonth);

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
