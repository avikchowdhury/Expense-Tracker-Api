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
    }
}
