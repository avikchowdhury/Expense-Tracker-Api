using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Shared.Constants;
using ExpenseTracker.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ExpenseTracker.Api.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<CategoryDto>> GetCategoriesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Categories.Query()
            .AsNoTracking()
            .Where(category => category.UserId == userId)
            .OrderBy(category => category.Name)
            .Select(MapCategory())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<VendorCategoryRuleDto>> GetVendorRulesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.VendorCategoryRules.Query()
            .AsNoTracking()
            .Where(rule => rule.UserId == userId)
            .Include(rule => rule.Category)
            .OrderBy(rule => rule.VendorPattern)
            .Select(rule => new VendorCategoryRuleDto
            {
                Id = rule.Id,
                CategoryId = rule.CategoryId,
                CategoryName = rule.Category != null ? rule.Category.Name : string.Empty,
                VendorPattern = rule.VendorPattern,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> AddCategoryAsync(int userId, CategoryDto categoryDto, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(categoryDto.Name);
        var exists = await _unitOfWork.Categories.Query()
            .AnyAsync(category => category.UserId == userId && category.Name == normalizedName, cancellationToken);

        if (exists)
        {
            throw new ApiRequestException(StatusCodes.Status409Conflict, ApplicationText.Categories.CategoryAlreadyExists);
        }

        var category = new Category
        {
            UserId = userId,
            Name = normalizedName
        };

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapCategory(category);
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(int userId, int categoryId, CategoryDto categoryDto, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(existingCategory => existingCategory.Id == categoryId && existingCategory.UserId == userId, cancellationToken);

        if (category == null)
        {
            return null;
        }

        var normalizedName = NormalizeName(categoryDto.Name);
        var exists = await _unitOfWork.Categories.Query()
            .AnyAsync(existingCategory =>
                existingCategory.UserId == userId &&
                existingCategory.Name == normalizedName &&
                existingCategory.Id != categoryId,
                cancellationToken);

        if (exists)
        {
            throw new ApiRequestException(StatusCodes.Status409Conflict, ApplicationText.Categories.CategoryAlreadyExists);
        }

        category.Name = normalizedName;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapCategory(category);
    }

    public async Task<bool> DeleteCategoryAsync(int userId, int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(existingCategory => existingCategory.Id == categoryId && existingCategory.UserId == userId, cancellationToken);

        if (category == null)
        {
            return false;
        }

        _unitOfWork.Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<VendorCategoryRuleDto> AddVendorRuleAsync(int userId, VendorCategoryRuleDto ruleDto, CancellationToken cancellationToken = default)
    {
        var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
        var category = await LoadCategoryAsync(userId, ruleDto.CategoryId, cancellationToken);
        if (category == null)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Validation.SelectValidCategory);
        }

        var exists = await _unitOfWork.VendorCategoryRules.Query()
            .AnyAsync(rule =>
                rule.UserId == userId &&
                rule.VendorPattern.ToLower() == normalizedPattern.ToLower(),
                cancellationToken);

        if (exists)
        {
            throw new ApiRequestException(StatusCodes.Status409Conflict, ApplicationText.Categories.VendorRuleAlreadyExists);
        }

        var rule = new VendorCategoryRule
        {
            UserId = userId,
            CategoryId = category.Id,
            VendorPattern = normalizedPattern,
            IsActive = ruleDto.IsActive
        };

        await _unitOfWork.VendorCategoryRules.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapVendorRule(rule, category.Name);
    }

    public async Task<VendorCategoryRuleDto?> UpdateVendorRuleAsync(int userId, int ruleId, VendorCategoryRuleDto ruleDto, CancellationToken cancellationToken = default)
    {
        var rule = await _unitOfWork.VendorCategoryRules.Query()
            .Include(existingRule => existingRule.Category)
            .FirstOrDefaultAsync(existingRule => existingRule.Id == ruleId && existingRule.UserId == userId, cancellationToken);

        if (rule == null)
        {
            return null;
        }

        var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
        var category = await LoadCategoryAsync(userId, ruleDto.CategoryId, cancellationToken);
        if (category == null)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Validation.SelectValidCategory);
        }

        var exists = await _unitOfWork.VendorCategoryRules.Query()
            .AnyAsync(existingRule =>
                existingRule.UserId == userId &&
                existingRule.VendorPattern.ToLower() == normalizedPattern.ToLower() &&
                existingRule.Id != ruleId,
                cancellationToken);

        if (exists)
        {
            throw new ApiRequestException(StatusCodes.Status409Conflict, ApplicationText.Categories.VendorRuleAlreadyExists);
        }

        rule.VendorPattern = normalizedPattern;
        rule.CategoryId = category.Id;
        rule.IsActive = ruleDto.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapVendorRule(rule, category.Name);
    }

    public async Task<bool> DeleteVendorRuleAsync(int userId, int ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _unitOfWork.VendorCategoryRules.Query()
            .FirstOrDefaultAsync(existingRule => existingRule.Id == ruleId && existingRule.UserId == userId, cancellationToken);

        if (rule == null)
        {
            return false;
        }

        _unitOfWork.VendorCategoryRules.Remove(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<Category?> LoadCategoryAsync(int userId, int categoryId, CancellationToken cancellationToken)
    {
        return _unitOfWork.Categories.Query()
            .FirstOrDefaultAsync(existingCategory => existingCategory.Id == categoryId && existingCategory.UserId == userId, cancellationToken);
    }

    private static string NormalizeName(string name) => name.Trim();

    private static string NormalizeVendorPattern(string vendorPattern) => vendorPattern.Trim();

    private static CategoryDto MapCategory(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    private static Expression<Func<Category, CategoryDto>> MapCategory()
    {
        return category => new CategoryDto
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    private static VendorCategoryRuleDto MapVendorRule(VendorCategoryRule rule, string categoryName)
    {
        return new VendorCategoryRuleDto
        {
            Id = rule.Id,
            CategoryId = rule.CategoryId,
            CategoryName = categoryName,
            VendorPattern = rule.VendorPattern,
            IsActive = rule.IsActive,
            CreatedAt = rule.CreatedAt
        };
    }
}
