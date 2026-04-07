using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;

        public AdminController(ExpenseTrackerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<AdminOverviewDto>> GetOverview()
        {
            var totalUsers = await _dbContext.Users.CountAsync();
            var adminCount = await _dbContext.Users.CountAsync(user => user.Role == "Admin");
            var receiptCount = await _dbContext.Receipts.CountAsync();
            var trackedReceiptSpend = await _dbContext.Receipts.SumAsync(receipt => (decimal?)receipt.TotalAmount) ?? 0m;

            return Ok(new AdminOverviewDto
            {
                TotalUsers = totalUsers,
                AdminCount = adminCount,
                StandardUserCount = totalUsers - adminCount,
                ReceiptCount = receiptCount,
                TrackedReceiptSpend = trackedReceiptSpend
            });
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<AdminUserSummaryDto>>> GetUsers()
        {
            var users = await _dbContext.Users
                .AsNoTracking()
                .OrderByDescending(user => user.Role == "Admin")
                .ThenBy(user => user.Email)
                .ToListAsync();

            var receiptSummaries = await _dbContext.Receipts
                .AsNoTracking()
                .GroupBy(receipt => receipt.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count(),
                    LatestReceiptAt = group.Max(receipt => (DateTime?)receipt.UploadedAt)
                })
                .ToDictionaryAsync(item => item.UserId);

            var budgetCounts = await _dbContext.Budgets
                .AsNoTracking()
                .GroupBy(budget => budget.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(item => item.UserId, item => item.Count);

            var categoryCounts = await _dbContext.Categories
                .AsNoTracking()
                .GroupBy(category => category.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(item => item.UserId, item => item.Count);

            var response = users.Select(user =>
            {
                receiptSummaries.TryGetValue(user.Id, out var receiptSummary);
                budgetCounts.TryGetValue(user.Id, out var budgetCount);
                categoryCounts.TryGetValue(user.Id, out var categoryCount);

                return BuildUserSummary(
                    user,
                    receiptSummary?.Count ?? 0,
                    budgetCount,
                    categoryCount,
                    receiptSummary?.LatestReceiptAt);
            });

            return Ok(response);
        }

        [HttpPut("users/{userId:int}/role")]
        public async Task<ActionResult<AdminUserSummaryDto>> UpdateUserRole(
            int userId,
            [FromBody] UpdateUserRoleDto request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId is null)
            {
                return Unauthorized();
            }

            var normalizedRole = NormalizeRole(request.Role);
            if (normalizedRole is null)
            {
                return BadRequest(new { message = "Role must be either Admin or User." });
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (user.Id == currentUserId.Value && !string.Equals(user.Role, normalizedRole, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Use another admin account to change your own role." });
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                && normalizedRole == "User")
            {
                var adminCount = await _dbContext.Users.CountAsync(existingUser => existingUser.Role == "Admin");
                if (adminCount <= 1)
                {
                    return BadRequest(new { message = "At least one admin account must remain in the workspace." });
                }
            }

            user.Role = normalizedRole;
            await _dbContext.SaveChangesAsync();

            var receiptCount = await _dbContext.Receipts.CountAsync(receipt => receipt.UserId == user.Id);
            var budgetCount = await _dbContext.Budgets.CountAsync(budget => budget.UserId == user.Id);
            var categoryCount = await _dbContext.Categories.CountAsync(category => category.UserId == user.Id);
            var latestReceiptAt = await _dbContext.Receipts
                .Where(receipt => receipt.UserId == user.Id)
                .MaxAsync(receipt => (DateTime?)receipt.UploadedAt);

            return Ok(BuildUserSummary(user, receiptCount, budgetCount, categoryCount, latestReceiptAt));
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private AdminUserSummaryDto BuildUserSummary(
            User user,
            int receiptCount,
            int budgetCount,
            int categoryCount,
            DateTime? latestReceiptAt)
        {
            return new AdminUserSummaryDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = NormalizeRole(user.Role) ?? "User",
                AvatarUrl = BuildAvatarUrl(user.AvatarUrl),
                ReceiptCount = receiptCount,
                BudgetCount = budgetCount,
                CategoryCount = categoryCount,
                LatestReceiptAt = latestReceiptAt
            };
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

        private static string? NormalizeRole(string? role)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "User";
            }

            return null;
        }
    }
}
