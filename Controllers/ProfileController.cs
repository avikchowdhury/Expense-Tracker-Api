using ExpenseTracker.Api.Data;
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
    [Route("api/profile")]
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
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return Ok(new { user.Email, user.Role, user.AvatarUrl });
        }

        [HttpPost("avatar")]
        [RequestSizeLimit(5_000_000)] // 5MB limit
        public async Task<IActionResult> UploadAvatar([FromForm] Dtos.AvatarUploadDto dto)
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (dto.File == null || dto.File.Length == 0) return BadRequest("No file uploaded");
            // Save to wwwroot/avatars/{userId}.ext (ensure wwwroot/avatars exists)
            var ext = System.IO.Path.GetExtension(dto.File.FileName);
            var fileName = $"{userId}{ext}";
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }
            user.AvatarUrl = $"/avatars/{fileName}";
            await _dbContext.SaveChangesAsync();
            return Ok(new { user.AvatarUrl });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email;
            await _dbContext.SaveChangesAsync();
            return Ok(new { user.Email, user.Role, user.AvatarUrl });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
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

    public class UpdateProfileDto
    {
        public string? Email { get; set; }
    }
}
