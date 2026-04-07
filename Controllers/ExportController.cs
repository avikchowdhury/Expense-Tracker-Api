using ExpenseTracker.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        public ExportController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("excel")]
        public async Task<IActionResult> ExportToExcel()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var expenses = await _dbContext.Expenses.Where(e => e.UserId == userId).ToListAsync();
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Expenses");
            ws.Cells[1, 1].Value = "Date";
            ws.Cells[1, 2].Value = "Amount";
            ws.Cells[1, 3].Value = "Category";
            ws.Cells[1, 4].Value = "Description";
            for (int i = 0; i < expenses.Count; i++)
            {
                ws.Cells[i + 2, 1].Value = expenses[i].Date;
                ws.Cells[i + 2, 2].Value = expenses[i].Amount;
                ws.Cells[i + 2, 3].Value = expenses[i].Category;
                ws.Cells[i + 2, 4].Value = expenses[i].Description;
            }
            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "expenses.xlsx");
        }
    }
}
