using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services
{
    public interface IReceiptService
    {
        Task<Receipt> StoreReceiptAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
        Task<Receipt> QuickAddReceiptAsync(int userId, string vendor, decimal amount, string category, DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<Receipt>> GetReceiptsForUserAsync(int userId);
        Task<Receipt?> GetReceiptByIdAsync(int userId, int receiptId);
        Task<Receipt?> UpdateReceiptAsync(int userId, int receiptId, string? category, string? parsedContentJson, CancellationToken cancellationToken = default);
        Task<bool> DeleteReceiptAsync(int userId, int receiptId, CancellationToken cancellationToken = default);
    }
}
