using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIService _aiService;

        public ExportController(IUnitOfWork unitOfWork, IAIService aiService)
        {
            _unitOfWork = unitOfWork;
            _aiService = aiService;
        }

        [HttpGet("excel")]
        public async Task<IActionResult> ExportToExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipts = await _unitOfWork.Receipts.Query().Where(r => r.UserId == userId).OrderByDescending(r => r.UploadedAt).ToListAsync();
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Receipts");
            ws.Cells[1, 1].Value = "Date";
            ws.Cells[1, 2].Value = "Vendor";
            ws.Cells[1, 3].Value = "Amount";
            ws.Cells[1, 4].Value = "Category";
            ws.Cells[1, 5].Value = "File Name";
            for (int i = 0; i < receipts.Count; i++)
            {
                ws.Cells[i + 2, 1].Value = receipts[i].UploadedAt.ToString("yyyy-MM-dd");
                ws.Cells[i + 2, 2].Value = receipts[i].Vendor ?? "Unknown";
                ws.Cells[i + 2, 3].Value = receipts[i].TotalAmount;
                ws.Cells[i + 2, 4].Value = receipts[i].Category ?? "Uncategorized";
                ws.Cells[i + 2, 5].Value = receipts[i].FileName;
            }
            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "receipts.xlsx");
        }

        [HttpGet("report")]
        public async Task<IActionResult> ExportAiReport()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var now = System.DateTime.UtcNow;
            var monthStart = new System.DateTime(now.Year, now.Month, 1);

            var receipts = await _unitOfWork.Receipts.Query()
                .Where(r => r.UserId == userId && r.UploadedAt >= monthStart)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            var summary = await _aiService.GetMonthlySummaryAsync(userId);
            var forecast = await _aiService.GetSpendingForecastAsync(userId);
            var vendorAnalysis = await _aiService.GetVendorAnalysisAsync(userId);

            using var package = new ExcelPackage();

            // Summary sheet
            var summarySheet = package.Workbook.Worksheets.Add("AI Summary");
            summarySheet.Cells[1, 1].Value = $"Monthly AI Report — {summary.Month}";
            summarySheet.Cells[1, 1].Style.Font.Bold = true;
            summarySheet.Cells[1, 1].Style.Font.Size = 14;
            summarySheet.Cells[3, 1].Value = "Total Spend";
            summarySheet.Cells[3, 2].Value = summary.TotalSpend;
            summarySheet.Cells[4, 1].Value = "Top Category";
            summarySheet.Cells[4, 2].Value = summary.TopCategory;
            summarySheet.Cells[5, 1].Value = "Receipt Count";
            summarySheet.Cells[5, 2].Value = summary.ReceiptCount;
            summarySheet.Cells[6, 1].Value = "Projected Month-End";
            summarySheet.Cells[6, 2].Value = forecast.ProjectedMonthEnd;
            summarySheet.Cells[8, 1].Value = "AI Narrative";
            summarySheet.Cells[8, 1].Style.Font.Bold = true;
            summarySheet.Cells[9, 1].Value = summary.AiSummary;
            summarySheet.Cells[9, 1].Style.WrapText = true;
            summarySheet.Cells[11, 1].Value = "Forecast";
            summarySheet.Cells[11, 1].Style.Font.Bold = true;
            summarySheet.Cells[12, 1].Value = forecast.AiNarrative;
            summarySheet.Cells[12, 1].Style.WrapText = true;
            summarySheet.Column(1).Width = 30;
            summarySheet.Column(2).Width = 20;

            // Receipts sheet
            var receiptsSheet = package.Workbook.Worksheets.Add("Receipts This Month");
            receiptsSheet.Cells[1, 1].Value = "Date";
            receiptsSheet.Cells[1, 2].Value = "Vendor";
            receiptsSheet.Cells[1, 3].Value = "Amount";
            receiptsSheet.Cells[1, 4].Value = "Category";
            for (int i = 0; i < receipts.Count; i++)
            {
                receiptsSheet.Cells[i + 2, 1].Value = receipts[i].UploadedAt.ToString("yyyy-MM-dd");
                receiptsSheet.Cells[i + 2, 2].Value = receipts[i].Vendor ?? "Unknown";
                receiptsSheet.Cells[i + 2, 3].Value = receipts[i].TotalAmount;
                receiptsSheet.Cells[i + 2, 4].Value = receipts[i].Category ?? "Uncategorized";
            }

            // Vendor Analysis sheet
            var vendorSheet = package.Workbook.Worksheets.Add("Vendor Analysis");
            vendorSheet.Cells[1, 1].Value = "Vendor";
            vendorSheet.Cells[1, 2].Value = "Total Spend";
            vendorSheet.Cells[1, 3].Value = "Visits";
            vendorSheet.Cells[1, 4].Value = "Avg Transaction";
            vendorSheet.Cells[1, 5].Value = "Trend";
            vendorSheet.Cells[1, 6].Value = "Change %";
            for (int i = 0; i < vendorAnalysis.TopVendors.Count; i++)
            {
                var v = vendorAnalysis.TopVendors[i];
                vendorSheet.Cells[i + 2, 1].Value = v.Vendor;
                vendorSheet.Cells[i + 2, 2].Value = v.TotalSpend;
                vendorSheet.Cells[i + 2, 3].Value = v.VisitCount;
                vendorSheet.Cells[i + 2, 4].Value = v.AverageTransaction;
                vendorSheet.Cells[i + 2, 5].Value = v.Trend;
                vendorSheet.Cells[i + 2, 6].Value = v.ChangePercent.HasValue ? $"{v.ChangePercent:F1}%" : "New";
            }
            vendorSheet.Cells[vendorAnalysis.TopVendors.Count + 3, 1].Value = "AI Observation:";
            vendorSheet.Cells[vendorAnalysis.TopVendors.Count + 3, 2].Value = vendorAnalysis.AiObservation;

            var reportStream = new MemoryStream(package.GetAsByteArray());
            var fileName = $"ai-report-{now:yyyy-MM}.xlsx";
            return File(reportStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
