using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReceiptsController : ControllerBase
    {
        private readonly IReceiptService _receiptService;

        public ReceiptsController(IReceiptService receiptService)
        {
            _receiptService = receiptService;
        }


        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadReceipt([FromForm] ReceiptUploadRequestDto request)
        {
            if (request?.File == null || request.File.Length == 0)
                return BadRequest("Please provide a receipt file.");
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipt = await _receiptService.StoreReceiptAsync(userId, request.File);
            if (!string.IsNullOrEmpty(request.Category)) receipt.Category = request.Category;
            if (!string.IsNullOrEmpty(request.Notes)) receipt.ParsedContentJson = request.Notes; // or add a Notes field if needed
            var dto = new ReceiptDto
            {
                Id = receipt.Id,
                UserId = receipt.UserId,
                UploadedAt = receipt.UploadedAt,
                FileName = receipt.FileName,
                BlobUrl = receipt.BlobUrl,
                TotalAmount = receipt.TotalAmount,
                Vendor = receipt.Vendor,
                Category = receipt.Category,
                ParsedContentJson = receipt.ParsedContentJson
            };
            return Ok(dto);
        }


        [HttpGet]
        public async Task<IActionResult> GetReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var query = (await _receiptService.GetReceiptsForUserAsync(userId)).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.FileName.Contains(search) || (x.Vendor != null && x.Vendor.Contains(search)));
            if (!string.IsNullOrEmpty(category))
                query = query.Where(x => x.Category == category);
            if (dateFrom.HasValue)
                query = query.Where(x => x.UploadedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(x => x.UploadedAt <= dateTo.Value);
            var total = query.Count();
            var receipts = query.OrderByDescending(x => x.UploadedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var dtos = receipts.Select(x => new ReceiptDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UploadedAt = x.UploadedAt,
                FileName = x.FileName,
                BlobUrl = x.BlobUrl,
                TotalAmount = x.TotalAmount,
                Vendor = x.Vendor,
                Category = x.Category,
                ParsedContentJson = x.ParsedContentJson
            });
            return Ok(new { total, data = dtos });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReceipt(int id, [FromBody] ReceiptDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipt = await _receiptService.GetReceiptByIdAsync(userId, id);
            if (receipt == null) return NotFound();
            receipt.Category = dto.Category;
            receipt.ParsedContentJson = dto.ParsedContentJson;
            // Persist changes
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var db = (ExpenseTracker.Api.Data.ExpenseTrackerDbContext)scope.ServiceProvider.GetService(typeof(ExpenseTracker.Api.Data.ExpenseTrackerDbContext));
                db.Receipts.Update(receipt);
                await db.SaveChangesAsync();
            }
            return Ok(dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceipt(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipt = await _receiptService.GetReceiptByIdAsync(userId, id);
            if (receipt == null) return NotFound();
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var db = (ExpenseTracker.Api.Data.ExpenseTrackerDbContext)scope.ServiceProvider.GetService(typeof(ExpenseTracker.Api.Data.ExpenseTrackerDbContext));
                db.Receipts.Remove(receipt);
                await db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> DownloadReceiptFile(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipt = await _receiptService.GetReceiptByIdAsync(userId, id);
            if (receipt == null || string.IsNullOrEmpty(receipt.BlobUrl) || !System.IO.File.Exists(receipt.BlobUrl))
                return NotFound();
            var fileBytes = await System.IO.File.ReadAllBytesAsync(receipt.BlobUrl);
            var contentType = "application/octet-stream";
            return File(fileBytes, contentType, receipt.FileName);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var receipt = await _receiptService.GetReceiptByIdAsync(userId, id);
            if (receipt == null)
                return NotFound();

            var dto = new ReceiptDto
            {
                Id = receipt.Id,
                UserId = receipt.UserId,
                UploadedAt = receipt.UploadedAt,
                FileName = receipt.FileName,
                BlobUrl = receipt.BlobUrl,
                TotalAmount = receipt.TotalAmount,
                Vendor = receipt.Vendor,
                Category = receipt.Category,
                ParsedContentJson = receipt.ParsedContentJson
            };

            return Ok(dto);
        }
    }
}
