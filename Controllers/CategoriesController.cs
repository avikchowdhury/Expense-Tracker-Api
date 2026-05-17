using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    [AppAuthorize]
    public class CategoriesController : AppControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }


        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            return Ok(await _categoryService.GetCategoriesAsync(CurrentUserId));
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetVendorRules()
        {
            return Ok(await _categoryService.GetVendorRulesAsync(CurrentUserId));
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory([FromBody] CategoryDto categoryDto)
        {
            var validationProblem = ValidateRequest(categoryDto);
            if (validationProblem is not null)
                return validationProblem;

            return Ok(await _categoryService.AddCategoryAsync(CurrentUserId, categoryDto));
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto categoryDto)
        {
            var validationProblem = ValidateRequest(categoryDto);
            if (validationProblem is not null)
                return validationProblem;

            var category = await _categoryService.UpdateCategoryAsync(CurrentUserId, id, categoryDto);
            if (category == null)
                return NotFound();

            return Ok(category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!await _categoryService.DeleteCategoryAsync(CurrentUserId, id))
                return NotFound();

            return NoContent();
        }

        [HttpPost("rules")]
        public async Task<IActionResult> AddVendorRule([FromBody] VendorCategoryRuleDto ruleDto)
        {
            var validationProblem = ValidateRequest(ruleDto);
            if (validationProblem is not null)
                return validationProblem;

            return Ok(await _categoryService.AddVendorRuleAsync(CurrentUserId, ruleDto));
        }

        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateVendorRule(int id, [FromBody] VendorCategoryRuleDto ruleDto)
        {
            var validationProblem = ValidateRequest(ruleDto);
            if (validationProblem is not null)
                return validationProblem;

            var rule = await _categoryService.UpdateVendorRuleAsync(CurrentUserId, id, ruleDto);
            if (rule == null)
                return NotFound();
            
            return Ok(rule);
        }

        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteVendorRule(int id)
        {
            if (!await _categoryService.DeleteVendorRuleAsync(CurrentUserId, id))
                return NotFound();

            return NoContent();
        }
    }
}
