using ExpenseTracker.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BudgetsStatusController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        public BudgetsStatusController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            var userId = int.Parse(User.Identity.Name);
            var budgets = await _dbContext.Budgets.Where(b => b.UserId == userId).ToListAsync();
            var alerts = budgets.Where(b => b.CurrentSpent > b.MonthlyLimit).Select(b => $"Budget exceeded for {b.Category}").ToList();
            return Ok(new { budgets, alerts });
        }
    }
}
