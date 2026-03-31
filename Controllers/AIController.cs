using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;

        public AIController(IAIService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("parse-receipt")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseReceipt([FromForm] Dtos.ParseReceiptRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("Please provide a receipt file.");
            }

            var result = await _aiService.ParseReceiptAsync(request.File);
            return Ok(result);
        }
    }
}
