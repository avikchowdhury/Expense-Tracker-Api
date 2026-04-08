using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly FileStoragePaths _storagePaths;

        public ProfileController(ExpenseTrackerDbContext dbContext, FileStoragePaths storagePaths)
        {
            _dbContext = dbContext;
            _storagePaths = storagePaths;
        }


        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return Ok(new
            {
                user.Email,
                user.Role,
                AvatarUrl = BuildAvatarUrl(user.AvatarUrl),
                user.FullName,
                user.Phone,
                user.Address
            });
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
            var ext = System.IO.Path.GetExtension(dto.File.FileName);
            var fileName = $"{userId}{ext}";
            Directory.CreateDirectory(_storagePaths.AvatarsPath);
            var path = Path.Combine(_storagePaths.AvatarsPath, fileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }
            user.AvatarUrl = $"/avatars/{fileName}";
            await _dbContext.SaveChangesAsync();
            return Ok(new { AvatarUrl = BuildAvatarUrl(user.AvatarUrl) });
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
            if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName;
            if (!string.IsNullOrWhiteSpace(dto.Phone)) user.Phone = dto.Phone;
            if (!string.IsNullOrWhiteSpace(dto.Address)) user.Address = dto.Address;
            await _dbContext.SaveChangesAsync();
            return Ok(new
            {
                user.Email,
                user.Role,
                AvatarUrl = BuildAvatarUrl(user.AvatarUrl),
                user.FullName,
                user.Phone,
                user.Address
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (HashPassword(dto.OldPassword) != user.PasswordHash)
                return BadRequest(new { message = "Old password is incorrect." });
            user.PasswordHash = HashPassword(dto.NewPassword);
            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashed);
        }

        private string? BuildAvatarUrl(string? storedAvatarUrl)
        {
            if (string.IsNullOrWhiteSpace(storedAvatarUrl))
            {
                return null;
            }

            if (Uri.TryCreate(storedAvatarUrl, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            var normalizedPath = storedAvatarUrl.StartsWith("/")
                ? storedAvatarUrl
                : $"/{storedAvatarUrl}";

            return $"{Request.Scheme}://{Request.Host}{normalizedPath}";
        }
    }
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
