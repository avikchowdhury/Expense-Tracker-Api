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
                ParsedContentJson = receipt.ParsedContentJson,
                IsMarkedDuplicate = receipt.IsMarkedDuplicate
            };
            return Ok(dto);
        }

        [HttpPost("quick-add")]
        public async Task<IActionResult> QuickAddReceipt([FromBody] QuickAddReceiptDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Vendor) || request.Amount <= 0)
                return BadRequest("Vendor and a positive amount are required.");

            var date = request.Date.HasValue
                ? request.Date.Value
                : DateTime.UtcNow;

            var receipt = await _receiptService.QuickAddReceiptAsync(
                userId, request.Vendor, request.Amount, request.Category ?? "Uncategorized", date);

            return Ok(new ReceiptDto
            {
                Id = receipt.Id,
                UserId = receipt.UserId,
                UploadedAt = receipt.UploadedAt,
                FileName = receipt.FileName,
                BlobUrl = receipt.BlobUrl,
                TotalAmount = receipt.TotalAmount,
                Vendor = receipt.Vendor,
                Category = receipt.Category,
                ParsedContentJson = receipt.ParsedContentJson,
                IsMarkedDuplicate = receipt.IsMarkedDuplicate
            });
        }


        [HttpGet]
        public async Task<IActionResult> GetReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null, [FromQuery] bool? markedDuplicate = null)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var result = await _receiptService.GetReceiptsPageAsync(userId, new ReceiptQueryDto
            {
                Page = page,
                PageSize = pageSize,
                Search = search,
                Category = category,
                DateFrom = dateFrom,
                DateTo = dateTo,
                MarkedDuplicate = markedDuplicate
            });
            return Ok(result);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReceipt(int id, [FromBody] ReceiptDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var receipt = await _receiptService.UpdateReceiptAsync(userId, id, dto.Category, dto.ParsedContentJson);
            if (receipt == null) return NotFound();
            dto.Id = receipt.Id;
            dto.UserId = receipt.UserId;
            dto.UploadedAt = receipt.UploadedAt;
            dto.FileName = receipt.FileName;
            dto.BlobUrl = receipt.BlobUrl;
            dto.TotalAmount = receipt.TotalAmount;
            dto.Vendor = receipt.Vendor;
            dto.IsMarkedDuplicate = receipt.IsMarkedDuplicate;
            return Ok(dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceipt(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var deleted = await _receiptService.DeleteReceiptAsync(userId, id);
            if (!deleted) return NotFound();
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
                ParsedContentJson = receipt.ParsedContentJson,
                IsMarkedDuplicate = receipt.IsMarkedDuplicate
            };

            return Ok(dto);
        }

        [HttpPost("bulk/categorize")]
        public async Task<IActionResult> BulkCategorize([FromBody] BulkCategorizeReceiptsDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Category))
                return BadRequest(new { message = "Category is required." });

            var result = await _receiptService.BulkCategorizeAsync(userId, request.ReceiptIds, request.Category);
            return Ok(result);
        }

        [HttpPost("bulk/delete")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkReceiptSelectionDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _receiptService.BulkDeleteAsync(userId, request.ReceiptIds);
            return Ok(result);
        }

        [HttpPost("bulk/apply-vendor-rules")]
        public async Task<IActionResult> BulkApplyVendorRules([FromBody] BulkReceiptSelectionDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _receiptService.BulkApplyVendorRulesAsync(userId, request.ReceiptIds);
            return Ok(result);
        }

        [HttpPost("bulk/mark-duplicates")]
        public async Task<IActionResult> BulkMarkDuplicates([FromBody] BulkMarkDuplicateReceiptsDto request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var result = await _receiptService.BulkMarkDuplicatesAsync(userId, request.ReceiptIds, request.MarkAsDuplicate);
            return Ok(result);
        }
    }
}
