using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyCollection<CategoryDto>> GetCategoriesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<VendorCategoryRuleDto>> GetVendorRulesAsync(int userId, CancellationToken cancellationToken = default);
    Task<CategoryDto> AddCategoryAsync(int userId, CategoryDto categoryDto, CancellationToken cancellationToken = default);
    Task<CategoryDto?> UpdateCategoryAsync(int userId, int categoryId, CategoryDto categoryDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(int userId, int categoryId, CancellationToken cancellationToken = default);
    Task<VendorCategoryRuleDto> AddVendorRuleAsync(int userId, VendorCategoryRuleDto ruleDto, CancellationToken cancellationToken = default);
    Task<VendorCategoryRuleDto?> UpdateVendorRuleAsync(int userId, int ruleId, VendorCategoryRuleDto ruleDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteVendorRuleAsync(int userId, int ruleId, CancellationToken cancellationToken = default);
}
