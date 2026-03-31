using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        public ProfileController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.Identity.Name);
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return Ok(new { user.Email, user.Role });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = int.Parse(User.Identity.Name);
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            // TODO: Hash and update password
            user.PasswordHash = dto.NewPassword; // Replace with hash
            await _dbContext.SaveChangesAsync();
            return Ok();
        }
    }
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
