using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class CategoriesController : AppControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public CategoriesController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }


        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _unitOfWork.Categories.Query()
                .Where(c => c.UserId == CurrentUserId)
                .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
                .ToListAsync();
            return Ok(categories);
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetVendorRules()
        {
            var rules = await _unitOfWork.VendorCategoryRules.Query()
                .Where(rule => rule.UserId == CurrentUserId)
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
                .ToListAsync();

            return Ok(rules);
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory([FromBody] CategoryDto categoryDto)
        {
            if (string.IsNullOrWhiteSpace(categoryDto.Name))
                return BadRequest("Category name is required.");
            var exists = await _unitOfWork.Categories.Query().AnyAsync(c => c.UserId == CurrentUserId && c.Name == categoryDto.Name);
            if (exists)
                return Conflict("Category already exists.");
            var category = new Category { UserId = CurrentUserId, Name = categoryDto.Name };
            await _unitOfWork.Categories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();
            categoryDto.Id = category.Id;
            return Ok(categoryDto);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto categoryDto)
        {
            if (string.IsNullOrWhiteSpace(categoryDto.Name))
                return BadRequest("Category name is required.");
            var category = await _unitOfWork.Categories.Query().FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
            if (category == null)
                return NotFound();
            // Prevent duplicate names
            var exists = await _unitOfWork.Categories.Query().AnyAsync(c => c.UserId == CurrentUserId && c.Name == categoryDto.Name && c.Id != id);
            if (exists)
                return Conflict("Category already exists.");
            category.Name = categoryDto.Name;
            await _unitOfWork.SaveChangesAsync();
            return Ok(new CategoryDto { Id = category.Id, Name = category.Name });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _unitOfWork.Categories.Query().FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
            if (category == null)
                return NotFound();
            _unitOfWork.Categories.Remove(category);
            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("rules")]
        public async Task<IActionResult> AddVendorRule([FromBody] VendorCategoryRuleDto ruleDto)
        {
            var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
                return BadRequest("Vendor pattern is required.");

            var category = await _unitOfWork.Categories.Query()
                .FirstOrDefaultAsync(existingCategory => existingCategory.Id == ruleDto.CategoryId && existingCategory.UserId == CurrentUserId);
            if (category == null)
                return BadRequest("Select a valid category.");

            var exists = await _unitOfWork.VendorCategoryRules.Query()
                .AnyAsync(rule => rule.UserId == CurrentUserId && rule.VendorPattern.ToLower() == normalizedPattern.ToLower());
            if (exists)
                return Conflict("A rule for this vendor pattern already exists.");

            var rule = new VendorCategoryRule
            {
                UserId = CurrentUserId,
                CategoryId = category.Id,
                VendorPattern = normalizedPattern,
                IsActive = ruleDto.IsActive
            };

            await _unitOfWork.VendorCategoryRules.AddAsync(rule);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new VendorCategoryRuleDto
            {
                Id = rule.Id,
                CategoryId = category.Id,
                CategoryName = category.Name,
                VendorPattern = rule.VendorPattern,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt
            });
        }

        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateVendorRule(int id, [FromBody] VendorCategoryRuleDto ruleDto)
        {
            var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
                return BadRequest("Vendor pattern is required.");

            var rule = await _unitOfWork.VendorCategoryRules.Query()
                .Include(existingRule => existingRule.Category)
                .FirstOrDefaultAsync(existingRule => existingRule.Id == id && existingRule.UserId == CurrentUserId);
            if (rule == null)
                return NotFound();

            var category = await _unitOfWork.Categories.Query()
                .FirstOrDefaultAsync(existingCategory => existingCategory.Id == ruleDto.CategoryId && existingCategory.UserId == CurrentUserId);
            if (category == null)
                return BadRequest("Select a valid category.");

            var exists = await _unitOfWork.VendorCategoryRules.Query().AnyAsync(existingRule =>
                existingRule.UserId == CurrentUserId &&
                existingRule.VendorPattern.ToLower() == normalizedPattern.ToLower() &&
                existingRule.Id != id);
            if (exists)
                return Conflict("A rule for this vendor pattern already exists.");

            rule.VendorPattern = normalizedPattern;
            rule.CategoryId = category.Id;
            rule.IsActive = ruleDto.IsActive;

            await _unitOfWork.SaveChangesAsync();

            return Ok(new VendorCategoryRuleDto
            {
                Id = rule.Id,
                CategoryId = category.Id,
                CategoryName = category.Name,
                VendorPattern = rule.VendorPattern,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt
            });
        }

        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteVendorRule(int id)
        {
            var rule = await _unitOfWork.VendorCategoryRules.Query()
                .FirstOrDefaultAsync(existingRule => existingRule.Id == id && existingRule.UserId == CurrentUserId);
            if (rule == null)
                return NotFound();

            _unitOfWork.VendorCategoryRules.Remove(rule);
            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }

        private static string NormalizeVendorPattern(string? vendorPattern)
        {
            return string.IsNullOrWhiteSpace(vendorPattern)
                ? string.Empty
                : vendorPattern.Trim();
        }
    }
}
