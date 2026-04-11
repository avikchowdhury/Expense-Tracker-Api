using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services
{
    public interface IReceiptService
    {
        Task<Receipt> StoreReceiptAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
        Task<Receipt> QuickAddReceiptAsync(int userId, string vendor, decimal amount, string category, DateTime date, CancellationToken cancellationToken = default);
        Task<ReceiptPageResultDto> GetReceiptsPageAsync(int userId, ReceiptQueryDto query, CancellationToken cancellationToken = default);
        Task<IEnumerable<Receipt>> GetReceiptsForUserAsync(int userId);
        Task<Receipt?> GetReceiptByIdAsync(int userId, int receiptId);
        Task<Receipt?> UpdateReceiptAsync(int userId, int receiptId, string? category, string? parsedContentJson, CancellationToken cancellationToken = default);
        Task<bool> DeleteReceiptAsync(int userId, int receiptId, CancellationToken cancellationToken = default);
        Task<BulkReceiptOperationResultDto> BulkCategorizeAsync(int userId, IReadOnlyCollection<int> receiptIds, string category, CancellationToken cancellationToken = default);
        Task<BulkReceiptOperationResultDto> BulkDeleteAsync(int userId, IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken = default);
        Task<BulkReceiptOperationResultDto> BulkApplyVendorRulesAsync(int userId, IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken = default);
        Task<BulkReceiptOperationResultDto> BulkMarkDuplicatesAsync(int userId, IReadOnlyCollection<int> receiptIds, bool isMarkedDuplicate, CancellationToken cancellationToken = default);
    }
}
