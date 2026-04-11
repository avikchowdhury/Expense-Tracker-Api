using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Extensions;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly FileStoragePaths _storagePaths;

        public ReceiptService(IUnitOfWork unitOfWork, FileStoragePaths storagePaths)
        {
            _unitOfWork = unitOfWork;
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

            var parsed = await ParseReceiptAsync(filePath, cancellationToken);
            receipt.ParsedContentJson = parsed;

            var parsedData = ParsingHelpers.ParseParsedReceipt(parsed);
            if (parsedData.TotalAmount.HasValue) receipt.TotalAmount = parsedData.TotalAmount.Value;
            if (!string.IsNullOrEmpty(parsedData.Vendor)) receipt.Vendor = parsedData.Vendor;
            if (!string.IsNullOrEmpty(parsedData.Category)) receipt.Category = parsedData.Category;

            var vendorRules = await LoadActiveVendorRulesAsync(userId, cancellationToken);
            ApplyVendorRule(receipt, vendorRules);

            await _unitOfWork.Receipts.AddAsync(receipt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SyncExpenseFromReceiptAsync(receipt, cancellationToken);

            return receipt;
        }

        public async Task<Receipt> QuickAddReceiptAsync(int userId, string vendor, decimal amount, string category, DateTime date, CancellationToken cancellationToken = default)
        {
            var receipt = new Receipt
            {
                UserId = userId,
                FileName = $"quick-add-{Guid.NewGuid():N}.txt",
                UploadedAt = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc),
                Vendor = vendor,
                TotalAmount = amount,
                Category = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim(),
                ParsedContentJson = "{\"source\":\"quick-add\"}"
            };

            var vendorRules = await LoadActiveVendorRulesAsync(userId, cancellationToken);
            ApplyVendorRule(receipt, vendorRules);

            await _unitOfWork.Receipts.AddAsync(receipt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SyncExpenseFromReceiptAsync(receipt, cancellationToken);

            return receipt;
        }

        public async Task<ReceiptPageResultDto> GetReceiptsPageAsync(int userId, ReceiptQueryDto query, CancellationToken cancellationToken = default)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var receiptQuery = _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{query.Search.Trim()}%";
                receiptQuery = receiptQuery.Where(x =>
                    EF.Functions.Like(x.FileName, pattern) ||
                    (x.Vendor != null && EF.Functions.Like(x.Vendor, pattern)));
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var normalizedCategory = query.Category.Trim();
                receiptQuery = receiptQuery.Where(x => x.Category == normalizedCategory);
            }

            if (query.DateFrom.HasValue)
            {
                receiptQuery = receiptQuery.Where(x => x.UploadedAt >= query.DateFrom.Value);
            }

            if (query.DateTo.HasValue)
            {
                receiptQuery = receiptQuery.Where(x => x.UploadedAt <= query.DateTo.Value);
            }

            if (query.MarkedDuplicate.HasValue)
            {
                receiptQuery = receiptQuery.Where(x => x.IsMarkedDuplicate == query.MarkedDuplicate.Value);
            }

            var total = await receiptQuery.CountAsync(cancellationToken);
            var data = await receiptQuery
                .OrderByDescending(x => x.UploadedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ReceiptDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UploadedAt = x.UploadedAt,
                    FileName = x.FileName,
                    BlobUrl = x.BlobUrl,
                    TotalAmount = x.TotalAmount,
                    Vendor = x.Vendor,
                    Category = x.Category,
                    ParsedContentJson = x.ParsedContentJson,
                    IsMarkedDuplicate = x.IsMarkedDuplicate
                })
                .ToListAsync(cancellationToken);

            return new ReceiptPageResultDto
            {
                Total = total,
                Data = data
            };
        }

        public async Task<IEnumerable<Receipt>> GetReceiptsForUserAsync(int userId)
        {
            return await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();
        }

        public async Task<Receipt?> GetReceiptByIdAsync(int userId, int receiptId)
        {
            return await _unitOfWork.Receipts.Query()
                .FirstOrDefaultAsync(x => x.Id == receiptId && x.UserId == userId);
        }

        public async Task<Receipt?> UpdateReceiptAsync(int userId, int receiptId, string? category, string? parsedContentJson, CancellationToken cancellationToken = default)
        {
            var receipt = await GetReceiptByIdAsync(userId, receiptId);
            if (receipt == null)
            {
                return null;
            }

            receipt.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            receipt.ParsedContentJson = parsedContentJson;

            var vendorRules = await LoadActiveVendorRulesAsync(userId, cancellationToken);
            ApplyVendorRule(receipt, vendorRules);

            _unitOfWork.Receipts.Update(receipt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            var linkedExpenses = await _unitOfWork.Expenses.Query()
                .Where(x => x.UserId == userId && x.ReceiptId == receiptId)
                .ToListAsync(cancellationToken);

            if (linkedExpenses.Count > 0)
            {
                _unitOfWork.Expenses.RemoveRange(linkedExpenses);
            }

            _unitOfWork.Receipts.Remove(receipt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<BulkReceiptOperationResultDto> BulkCategorizeAsync(int userId, IReadOnlyCollection<int> receiptIds, string category, CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeReceiptIds(receiptIds);
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();

            if (normalizedIds.Count == 0)
            {
                return BuildBulkResult(0, 0, "Select at least one receipt.");
            }

            var receipts = await _unitOfWork.Receipts.Query()
                .Where(x => x.UserId == userId && normalizedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var receipt in receipts)
            {
                receipt.Category = normalizedCategory;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var receipt in receipts)
            {
                await SyncExpenseFromReceiptAsync(receipt, cancellationToken);
            }

            return BuildBulkResult(normalizedIds.Count, receipts.Count, $"Updated {receipts.Count} receipt categor{(receipts.Count == 1 ? "y" : "ies")}.");
        }

        public async Task<BulkReceiptOperationResultDto> BulkDeleteAsync(int userId, IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeReceiptIds(receiptIds);
            if (normalizedIds.Count == 0)
            {
                return BuildBulkResult(0, 0, "Select at least one receipt.");
            }

            var receipts = await _unitOfWork.Receipts.Query()
                .Where(x => x.UserId == userId && normalizedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var linkedExpenses = await _unitOfWork.Expenses.Query()
                .Where(x => x.UserId == userId && x.ReceiptId.HasValue && normalizedIds.Contains(x.ReceiptId.Value))
                .ToListAsync(cancellationToken);

            if (linkedExpenses.Count > 0)
            {
                _unitOfWork.Expenses.RemoveRange(linkedExpenses);
            }

            if (receipts.Count > 0)
            {
                _unitOfWork.Receipts.RemoveRange(receipts);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return BuildBulkResult(normalizedIds.Count, receipts.Count, $"Deleted {receipts.Count} receipt{(receipts.Count == 1 ? string.Empty : "s")}.");
        }

        public async Task<BulkReceiptOperationResultDto> BulkApplyVendorRulesAsync(int userId, IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeReceiptIds(receiptIds);
            if (normalizedIds.Count == 0)
            {
                return BuildBulkResult(0, 0, "Select at least one receipt.");
            }

            var receipts = await _unitOfWork.Receipts.Query()
                .Where(x => x.UserId == userId && normalizedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var vendorRules = await LoadActiveVendorRulesAsync(userId, cancellationToken);
            var updatedReceipts = new List<Receipt>();

            foreach (var receipt in receipts)
            {
                var originalCategory = receipt.Category;
                ApplyVendorRule(receipt, vendorRules);
                if (!string.Equals(originalCategory, receipt.Category, StringComparison.OrdinalIgnoreCase))
                {
                    updatedReceipts.Add(receipt);
                }
            }

            if (updatedReceipts.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                foreach (var receipt in updatedReceipts)
                {
                    await SyncExpenseFromReceiptAsync(receipt, cancellationToken);
                }
            }

            return BuildBulkResult(normalizedIds.Count, updatedReceipts.Count, $"Applied vendor rules to {updatedReceipts.Count} receipt{(updatedReceipts.Count == 1 ? string.Empty : "s")}.");
        }

        public async Task<BulkReceiptOperationResultDto> BulkMarkDuplicatesAsync(int userId, IReadOnlyCollection<int> receiptIds, bool isMarkedDuplicate, CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeReceiptIds(receiptIds);
            if (normalizedIds.Count == 0)
            {
                return BuildBulkResult(0, 0, "Select at least one receipt.");
            }

            var receipts = await _unitOfWork.Receipts.Query()
                .Where(x => x.UserId == userId && normalizedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var receipt in receipts)
            {
                receipt.IsMarkedDuplicate = isMarkedDuplicate;
            }

            if (receipts.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var action = isMarkedDuplicate ? "Marked" : "Cleared";
            return BuildBulkResult(normalizedIds.Count, receipts.Count, $"{action} {receipts.Count} duplicate flag{(receipts.Count == 1 ? string.Empty : "s")}.");
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
            var expense = await _unitOfWork.Expenses.Query()
                .FirstOrDefaultAsync(x => x.UserId == receipt.UserId && x.ReceiptId == receipt.Id, cancellationToken);

            var category = await ResolveCategoryAsync(receipt.UserId, receipt.Category, cancellationToken);

            if (expense == null)
            {
                expense = new Expense
                {
                    UserId = receipt.UserId,
                    ReceiptId = receipt.Id
                };
                await _unitOfWork.Expenses.AddAsync(expense, cancellationToken);
            }

            expense.Date = receipt.UploadedAt;
            expense.Amount = receipt.TotalAmount;
            expense.CategoryId = category?.Id;
            expense.Description = !string.IsNullOrWhiteSpace(receipt.Vendor) ? receipt.Vendor : receipt.FileName;
            expense.Currency = "USD";

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<ActiveVendorRule>> LoadActiveVendorRulesAsync(int userId, CancellationToken cancellationToken)
        {
            return await _unitOfWork.VendorCategoryRules.Query()
                .AsNoTracking()
                .Where(rule => rule.UserId == userId && rule.IsActive && rule.Category != null)
                .OrderByDescending(rule => rule.VendorPattern.Length)
                .Select(rule => new ActiveVendorRule(rule.VendorPattern, rule.Category != null ? rule.Category.Name : string.Empty))
                .ToListAsync(cancellationToken);
        }

        private static void ApplyVendorRule(Receipt receipt, IReadOnlyList<ActiveVendorRule> vendorRules)
        {
            if (string.IsNullOrWhiteSpace(receipt.Vendor))
            {
                return;
            }

            var normalizedVendor = receipt.Vendor.Trim().ToLowerInvariant();
            var matchingRule = vendorRules.FirstOrDefault(rule =>
                !string.IsNullOrWhiteSpace(rule.CategoryName) &&
                normalizedVendor.Contains(rule.VendorPattern.ToLowerInvariant()));

            if (matchingRule != null)
            {
                receipt.Category = matchingRule.CategoryName;
            }
        }

        private async Task<Category?> ResolveCategoryAsync(int userId, string? categoryName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = categoryName.Trim();
            var category = await _unitOfWork.Categories.Query()
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

            await _unitOfWork.Categories.AddAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return category;
        }

        private static List<int> NormalizeReceiptIds(IEnumerable<int>? receiptIds)
        {
            return receiptIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? new List<int>();
        }

        private static BulkReceiptOperationResultDto BuildBulkResult(int requestedCount, int affectedCount, string message)
        {
            return new BulkReceiptOperationResultDto
            {
                RequestedCount = requestedCount,
                AffectedCount = affectedCount,
                Message = message
            };
        }

        private sealed record ActiveVendorRule(string VendorPattern, string CategoryName);
    }
}
