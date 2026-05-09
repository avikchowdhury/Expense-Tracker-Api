using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class BudgetsStatusController : AppControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public BudgetsStatusController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            var budgets = await _unitOfWork.Budgets.Query().Where(b => b.UserId == CurrentUserId).ToListAsync();
            var alerts = budgets.Where(b => b.CurrentSpent > b.MonthlyLimit).Select(b => $"Budget exceeded for {b.Category}").ToList();
            return Ok(new { budgets, alerts });
        }
    }
}
