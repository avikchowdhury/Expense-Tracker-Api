using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
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
    [Route("api/profile")]
    [AppAuthorize]
    public class ProfileController : AppControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly FileStoragePaths _storagePaths;

        public ProfileController(IUnitOfWork unitOfWork, FileStoragePaths storagePaths)
        {
            _unitOfWork = unitOfWork;
            _storagePaths = storagePaths;
        }


        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _unitOfWork.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            return Ok(MapProfile(user, RequestUser.PrimaryRole));
        }

        [HttpPost("avatar")]
        [RequestSizeLimit(5_000_000)] // 5MB limit
        public async Task<IActionResult> UploadAvatar([FromForm] Dtos.AvatarUploadDto dto)
        {
            var user = await _unitOfWork.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (dto.File == null || dto.File.Length == 0) return BadRequest("No file uploaded");
            var ext = System.IO.Path.GetExtension(dto.File.FileName);
            var fileName = $"{CurrentUserId}{ext}";
            Directory.CreateDirectory(_storagePaths.AvatarsPath);
            var path = Path.Combine(_storagePaths.AvatarsPath, fileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }
            user.AvatarUrl = $"/avatars/{fileName}";
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { AvatarUrl = BuildAvatarUrl(user.AvatarUrl) });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await _unitOfWork.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email;
            if (dto.FullName is not null) user.FullName = string.IsNullOrWhiteSpace(dto.FullName) ? null : dto.FullName.Trim();
            if (dto.Address is not null) user.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();

            if (dto.Phone is not null)
            {
                var normalizedPhone = NormalizePhone(dto.Phone);
                if (normalizedPhone is null && !string.IsNullOrWhiteSpace(dto.Phone))
                {
                    return BadRequest(new { message = "Phone number must include a country code and a 10-digit number." });
                }

                user.Phone = normalizedPhone;
            }

            if (dto.BudgetNotificationsEnabled.HasValue)
                user.BudgetNotificationsEnabled = dto.BudgetNotificationsEnabled.Value;
            if (dto.AnomalyNotificationsEnabled.HasValue)
                user.AnomalyNotificationsEnabled = dto.AnomalyNotificationsEnabled.Value;
            if (dto.SubscriptionNotificationsEnabled.HasValue)
                user.SubscriptionNotificationsEnabled = dto.SubscriptionNotificationsEnabled.Value;
            if (dto.WeeklySummaryEmailEnabled.HasValue)
                user.WeeklySummaryEmailEnabled = dto.WeeklySummaryEmailEnabled.Value;
            if (dto.MonthlyReportEmailEnabled.HasValue)
                user.MonthlyReportEmailEnabled = dto.MonthlyReportEmailEnabled.Value;
            if (dto.WeeklySummaryDay is not null)
                user.WeeklySummaryDay = NormalizeWeeklySummaryDay(dto.WeeklySummaryDay);

            await _unitOfWork.SaveChangesAsync();
            return Ok(MapProfile(user, RequestUser.PrimaryRole));
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await _unitOfWork.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (HashPassword(dto.OldPassword) != user.PasswordHash)
                return BadRequest(new { message = "Old password is incorrect." });
            user.PasswordHash = HashPassword(dto.NewPassword);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashed);
        }

        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var normalized = phone.Trim();
            var withoutPlus = normalized.StartsWith("+")
                ? normalized[1..]
                : normalized;

            if (withoutPlus.Any(character => !char.IsDigit(character)))
            {
                return null;
            }

            if (withoutPlus.Length < 11 || withoutPlus.Length > 14)
            {
                return null;
            }

            var countryCodeLength = withoutPlus.Length - 10;
            if (countryCodeLength < 1 || countryCodeLength > 4)
            {
                return null;
            }

            return $"+{withoutPlus}";
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

        private object MapProfile(User user, string role) => new
        {
            user.Email,
            Role = role,
            AvatarUrl = BuildAvatarUrl(user.AvatarUrl),
            user.FullName,
            user.Phone,
            user.Address,
            user.BudgetNotificationsEnabled,
            user.AnomalyNotificationsEnabled,
            user.SubscriptionNotificationsEnabled,
            user.WeeklySummaryEmailEnabled,
            user.MonthlyReportEmailEnabled,
            user.WeeklySummaryDay
        };

        private static string NormalizeWeeklySummaryDay(string? day)
        {
            if (string.IsNullOrWhiteSpace(day))
            {
                return "Monday";
            }

            return day.Trim().ToLowerInvariant() switch
            {
                "monday" => "Monday",
                "tuesday" => "Tuesday",
                "wednesday" => "Wednesday",
                "thursday" => "Thursday",
                "friday" => "Friday",
                "saturday" => "Saturday",
                "sunday" => "Sunday",
                _ => "Monday"
            };
        }
    }
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
