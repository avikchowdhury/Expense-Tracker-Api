using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Services
{
    public interface IReceiptService
    {
        Task<Receipt> StoreReceiptAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
        Task<IEnumerable<Receipt>> GetReceiptsForUserAsync(int userId);
        Task<Receipt?> GetReceiptByIdAsync(int userId, int receiptId);
    }
}
