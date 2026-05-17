using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class BulkReceiptSelectionDto
    {
        [Required]
        [NotEmptyCollection(ErrorMessage = ApplicationText.Receipts.SelectAtLeastOneReceipt)]
        public List<int> ReceiptIds { get; set; } = new();
    }

    public class BulkCategorizeReceiptsDto : BulkReceiptSelectionDto
    {
        [Required]
        [NotBlank(ErrorMessage = ApplicationText.Validation.CategoryRequired)]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;
    }

    public class BulkMarkDuplicateReceiptsDto : BulkReceiptSelectionDto
    {
        public bool MarkAsDuplicate { get; set; } = true;
    }

    public class BulkReceiptOperationResultDto
    {
        public int RequestedCount { get; set; }
        public int AffectedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ReceiptQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? Category { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? MarkedDuplicate { get; set; }
    }

    public class ReceiptPageResultDto
    {
        public int Total { get; set; }
        public List<ReceiptDto> Data { get; set; } = new();
    }
}
