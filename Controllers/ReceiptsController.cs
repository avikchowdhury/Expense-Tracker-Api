using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class ReceiptsController : AppControllerBase
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
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var receipt = await _receiptService.StoreReceiptAsync(CurrentUserId, request.File);
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
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var date = request.Date.HasValue
                ? request.Date.Value
                : DateTime.UtcNow;

            var receipt = await _receiptService.QuickAddReceiptAsync(
                CurrentUserId,
                request.Vendor,
                request.Amount,
                request.Category ?? ApplicationText.Defaults.UncategorizedCategory,
                date);

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
            var result = await _receiptService.GetReceiptsPageAsync(CurrentUserId, new ReceiptQueryDto
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
            var receipt = await _receiptService.UpdateReceiptAsync(CurrentUserId, id, dto.Category, dto.ParsedContentJson);
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
            var deleted = await _receiptService.DeleteReceiptAsync(CurrentUserId, id);
            if (!deleted) return NotFound();
            return Ok(new { success = true });
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> DownloadReceiptFile(int id)
        {
            var receipt = await _receiptService.GetReceiptByIdAsync(CurrentUserId, id);
            if (receipt == null || string.IsNullOrEmpty(receipt.BlobUrl) || !System.IO.File.Exists(receipt.BlobUrl))
                return NotFound();
            var fileBytes = await System.IO.File.ReadAllBytesAsync(receipt.BlobUrl);
            var contentType = "application/octet-stream";
            return File(fileBytes, contentType, receipt.FileName);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            var receipt = await _receiptService.GetReceiptByIdAsync(CurrentUserId, id);
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
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var result = await _receiptService.BulkCategorizeAsync(CurrentUserId, request.ReceiptIds, request.Category);
            return Ok(result);
        }

        [HttpPost("bulk/delete")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkReceiptSelectionDto request)
        {
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var result = await _receiptService.BulkDeleteAsync(CurrentUserId, request.ReceiptIds);
            return Ok(result);
        }

        [HttpPost("bulk/apply-vendor-rules")]
        public async Task<IActionResult> BulkApplyVendorRules([FromBody] BulkReceiptSelectionDto request)
        {
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var result = await _receiptService.BulkApplyVendorRulesAsync(CurrentUserId, request.ReceiptIds);
            return Ok(result);
        }

        [HttpPost("bulk/mark-duplicates")]
        public async Task<IActionResult> BulkMarkDuplicates([FromBody] BulkMarkDuplicateReceiptsDto request)
        {
            var validationProblem = ValidateRequest(request);
            if (validationProblem is not null)
                return validationProblem;

            var result = await _receiptService.BulkMarkDuplicatesAsync(CurrentUserId, request.ReceiptIds, request.MarkAsDuplicate);
            return Ok(result);
        }
    }
}
