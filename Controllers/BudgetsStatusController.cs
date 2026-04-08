using ExpenseTracker.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BudgetsStatusController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public BudgetsStatusController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgetStatus()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var budgets = await _unitOfWork.Budgets.Query().Where(b => b.UserId == userId).ToListAsync();
            var alerts = budgets.Where(b => b.CurrentSpent > b.MonthlyLimit).Select(b => $"Budget exceeded for {b.Category}").ToList();
            return Ok(new { budgets, alerts });
        }
    }
}
