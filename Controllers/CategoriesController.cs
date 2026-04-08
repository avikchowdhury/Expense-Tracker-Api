using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        public CategoriesController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var categories = await _dbContext.Categories
                .Where(c => c.UserId == userId)
                .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
                .ToListAsync();
            return Ok(categories);
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetVendorRules()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var rules = await _dbContext.VendorCategoryRules
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
                .ToListAsync();

            return Ok(rules);
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory([FromBody] CategoryDto categoryDto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            if (string.IsNullOrWhiteSpace(categoryDto.Name))
                return BadRequest("Category name is required.");
            var exists = await _dbContext.Categories.AnyAsync(c => c.UserId == userId && c.Name == categoryDto.Name);
            if (exists)
                return Conflict("Category already exists.");
            var category = new Category { UserId = userId, Name = categoryDto.Name };
            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();
            categoryDto.Id = category.Id;
            return Ok(categoryDto);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto categoryDto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            if (string.IsNullOrWhiteSpace(categoryDto.Name))
                return BadRequest("Category name is required.");
            var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (category == null)
                return NotFound();
            // Prevent duplicate names
            var exists = await _dbContext.Categories.AnyAsync(c => c.UserId == userId && c.Name == categoryDto.Name && c.Id != id);
            if (exists)
                return Conflict("Category already exists.");
            category.Name = categoryDto.Name;
            await _dbContext.SaveChangesAsync();
            return Ok(new CategoryDto { Id = category.Id, Name = category.Name });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (category == null)
                return NotFound();
            _dbContext.Categories.Remove(category);
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("rules")]
        public async Task<IActionResult> AddVendorRule([FromBody] VendorCategoryRuleDto ruleDto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
                return BadRequest("Vendor pattern is required.");

            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(existingCategory => existingCategory.Id == ruleDto.CategoryId && existingCategory.UserId == userId);
            if (category == null)
                return BadRequest("Select a valid category.");

            var exists = await _dbContext.VendorCategoryRules
                .AnyAsync(rule => rule.UserId == userId && rule.VendorPattern.ToLower() == normalizedPattern.ToLower());
            if (exists)
                return Conflict("A rule for this vendor pattern already exists.");

            var rule = new VendorCategoryRule
            {
                UserId = userId,
                CategoryId = category.Id,
                VendorPattern = normalizedPattern,
                IsActive = ruleDto.IsActive
            };

            _dbContext.VendorCategoryRules.Add(rule);
            await _dbContext.SaveChangesAsync();

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
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var normalizedPattern = NormalizeVendorPattern(ruleDto.VendorPattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
                return BadRequest("Vendor pattern is required.");

            var rule = await _dbContext.VendorCategoryRules
                .Include(existingRule => existingRule.Category)
                .FirstOrDefaultAsync(existingRule => existingRule.Id == id && existingRule.UserId == userId);
            if (rule == null)
                return NotFound();

            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(existingCategory => existingCategory.Id == ruleDto.CategoryId && existingCategory.UserId == userId);
            if (category == null)
                return BadRequest("Select a valid category.");

            var exists = await _dbContext.VendorCategoryRules.AnyAsync(existingRule =>
                existingRule.UserId == userId &&
                existingRule.VendorPattern.ToLower() == normalizedPattern.ToLower() &&
                existingRule.Id != id);
            if (exists)
                return Conflict("A rule for this vendor pattern already exists.");

            rule.VendorPattern = normalizedPattern;
            rule.CategoryId = category.Id;
            rule.IsActive = ruleDto.IsActive;

            await _dbContext.SaveChangesAsync();

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
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            var rule = await _dbContext.VendorCategoryRules
                .FirstOrDefaultAsync(existingRule => existingRule.Id == id && existingRule.UserId == userId);
            if (rule == null)
                return NotFound();

            _dbContext.VendorCategoryRules.Remove(rule);
            await _dbContext.SaveChangesAsync();
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
