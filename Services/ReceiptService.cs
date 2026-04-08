using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Extensions;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly FileStoragePaths _storagePaths;

        public ReceiptService(ExpenseTrackerDbContext dbContext, FileStoragePaths storagePaths)
        {
            _dbContext = dbContext;
            _storagePaths = storagePaths;
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

            Directory.CreateDirectory(_storagePaths.ReceiptsPath);

            var filePath = Path.Combine(_storagePaths.ReceiptsPath, $"{Guid.NewGuid()}_{file.FileName}");
            await using (var stream = File.Create(filePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            receipt.BlobUrl = filePath;

            // TODO: Replace with Azure AI / OpenAI receipt parser call
            var parsed= await ParseReceiptAsync(filePath, cancellationToken);
            receipt.ParsedContentJson = parsed; // as json string

            var parsedData = ParsingHelpers.ParseParsedReceipt(parsed);
            if (parsedData.TotalAmount.HasValue) receipt.TotalAmount = parsedData.TotalAmount.Value;
            if (!string.IsNullOrEmpty(parsedData.Vendor)) receipt.Vendor = parsedData.Vendor;
            if (!string.IsNullOrEmpty(parsedData.Category)) receipt.Category = parsedData.Category;

            await ApplyVendorRuleAsync(receipt, cancellationToken);

            _dbContext.Receipts.Add(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await SyncExpenseFromReceiptAsync(receipt, cancellationToken);

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

        public async Task<Receipt?> UpdateReceiptAsync(int userId, int receiptId, string? category, string? parsedContentJson, CancellationToken cancellationToken = default)
        {
            var receipt = await GetReceiptByIdAsync(userId, receiptId);
            if (receipt == null)
            {
                return null;
            }

            receipt.Category = category;
            receipt.ParsedContentJson = parsedContentJson;

            await ApplyVendorRuleAsync(receipt, cancellationToken);

            _dbContext.Receipts.Update(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await SyncExpenseFromReceiptAsync(receipt, cancellationToken);

            return receipt;
        }

        public async Task<bool> DeleteReceiptAsync(int userId, int receiptId, CancellationToken cancellationToken = default)
        {
            var receipt = await GetReceiptByIdAsync(userId, receiptId);
            if (receipt == null)
            {
                return false;
            }

            var linkedExpenses = await _dbContext.Expenses
                .Where(x => x.UserId == userId && x.ReceiptId == receiptId)
                .ToListAsync(cancellationToken);

            if (linkedExpenses.Count > 0)
            {
                _dbContext.Expenses.RemoveRange(linkedExpenses);
            }

            _dbContext.Receipts.Remove(receipt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        private Task<string> ParseReceiptAsync(string filePath, CancellationToken cancellationToken)
        {
            var fallback = ReceiptFallbackHelper.Parse(filePath);

            var result = new
            {
                Vendor = fallback.Vendor,
                TotalAmount = fallback.Amount,
                Category = fallback.Category,
                Date = fallback.Date,
                RawText = fallback.RawText,
                Items = fallback.Items
            };

            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(result));
        }

        private async Task SyncExpenseFromReceiptAsync(Receipt receipt, CancellationToken cancellationToken)
        {
            var expense = await _dbContext.Expenses
                .FirstOrDefaultAsync(x => x.UserId == receipt.UserId && x.ReceiptId == receipt.Id, cancellationToken);

            var category = await ResolveCategoryAsync(receipt.UserId, receipt.Category, cancellationToken);

            if (expense == null)
            {
                expense = new Expense
                {
                    UserId = receipt.UserId,
                    ReceiptId = receipt.Id
                };
                _dbContext.Expenses.Add(expense);
            }

            expense.Date = receipt.UploadedAt;
            expense.Amount = receipt.TotalAmount;
            expense.CategoryId = category?.Id;
            expense.Description = !string.IsNullOrWhiteSpace(receipt.Vendor) ? receipt.Vendor : receipt.FileName;
            expense.Currency = "USD";

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task ApplyVendorRuleAsync(Receipt receipt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(receipt.Vendor))
            {
                return;
            }

            var normalizedVendor = receipt.Vendor.Trim().ToLowerInvariant();
            var matchingRule = await _dbContext.VendorCategoryRules
                .Include(rule => rule.Category)
                .Where(rule => rule.UserId == receipt.UserId && rule.IsActive)
                .OrderByDescending(rule => rule.VendorPattern.Length)
                .FirstOrDefaultAsync(
                    rule => normalizedVendor.Contains(rule.VendorPattern.ToLower()),
                    cancellationToken);

            if (matchingRule?.Category != null)
            {
                receipt.Category = matchingRule.Category.Name;
            }
        }

        private async Task<Category?> ResolveCategoryAsync(int userId, string? categoryName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = categoryName.Trim();
            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == normalized, cancellationToken);

            if (category != null)
            {
                return category;
            }

            category = new Category
            {
                UserId = userId,
                Name = normalized
            };

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return category;
        }
    }
}
