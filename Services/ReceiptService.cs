using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Extensions;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public ReceiptService(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Receipt> StoreReceiptAsync(int userId, IFormFile file, CancellationToken cancellationToken = default)
        {
            var receipt = new Receipt
            {
                UserId = userId,
                FileName = file.FileName,
                UploadedAt = DateTime.UtcNow,
                Category = "Uncategorized",
                TotalAmount = 0M,
                ParsedContentJson = "{}"
            };

            var uploads = Path.Combine(Path.GetTempPath(), "receipt_uploads");
            Directory.CreateDirectory(uploads);

            var filePath = Path.Combine(uploads, $"{Guid.NewGuid()}_{file.FileName}");
            await using var stream = File.Create(filePath);
            await file.CopyToAsync(stream, cancellationToken);

            receipt.BlobUrl = filePath;

            // TODO: Replace with Azure AI / OpenAI receipt parser call
            var parsed= await ParseReceiptAsync(filePath, cancellationToken);
            receipt.ParsedContentJson = parsed; // as json string

            var parsedData = ParsingHelpers.ParseParsedReceipt(parsed);
            if (parsedData.TotalAmount.HasValue) receipt.TotalAmount = parsedData.TotalAmount.Value;
            if (!string.IsNullOrEmpty(parsedData.Vendor)) receipt.Vendor = parsedData.Vendor;
            if (!string.IsNullOrEmpty(parsedData.Category)) receipt.Category = parsedData.Category;

            _dbContext.Receipts.Add(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return receipt;
        }

        public async Task<Receipt?> GetReceiptByIdAsync(int userId, int receiptId)
        {
            return await _dbContext.Receipts
                .FirstOrDefaultAsync(x => x.Id == receiptId && x.UserId == userId);
        }

        public async Task<IEnumerable<Receipt>> GetReceiptsForUserAsync(int userId)
        {
            return await _dbContext.Receipts
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();
        }

        private Task<string> ParseReceiptAsync(string filePath, CancellationToken cancellationToken)
        {
            // Mocked parsing as placeholder
            var result = new
            {
                Vendor = "Unknown Vendor",
                TotalAmount = 0.0M,
                Category = "Uncategorized",
                Items = new object[] { }
            };

            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(result));
        }
    }
}
