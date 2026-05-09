using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/admin")]
    [AppAuthorize(AppRoles.Admin)]
    public class AdminController : AppControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserRoleService _userRoleService;

        public AdminController(IUnitOfWork unitOfWork, IUserRoleService userRoleService)
        {
            _unitOfWork = unitOfWork;
            _userRoleService = userRoleService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<AdminOverviewDto>> GetOverview()
        {
            var totalUsers = await _unitOfWork.Users.Query().CountAsync();
            var adminCount = await _userRoleService.CountUsersInRoleAsync(AppRoles.Admin);
            var receiptCount = await _unitOfWork.Receipts.Query().CountAsync();
            var trackedReceiptSpend = await _unitOfWork.Receipts.Query().SumAsync(receipt => (decimal?)receipt.TotalAmount) ?? 0m;

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
            var users = await _unitOfWork.Users.Query()
                .AsNoTracking()
                .OrderByDescending(user => user.Role == "Admin")
                .ThenBy(user => user.Email)
                .ToListAsync();

            var receiptSummaries = await _unitOfWork.Receipts.Query()
                .AsNoTracking()
                .GroupBy(receipt => receipt.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count(),
                    LatestReceiptAt = group.Max(receipt => (DateTime?)receipt.UploadedAt)
                })
                .ToDictionaryAsync(item => item.UserId);

            var budgetCounts = await _unitOfWork.Budgets.Query()
                .AsNoTracking()
                .GroupBy(budget => budget.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(item => item.UserId, item => item.Count);

            var categoryCounts = await _unitOfWork.Categories.Query()
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
            var normalizedRole = _userRoleService.NormalizeRole(request.Role);
            if (normalizedRole is null)
            {
                return BadRequest(new { message = "Role must be either Admin or User." });
            }

            var user = await _unitOfWork.Users.FindAsync(userId);
            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (user.Id == CurrentUserId && !string.Equals(user.Role, normalizedRole, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Use another admin account to change your own role." });
            }

            if (string.Equals(user.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                && normalizedRole == AppRoles.User)
            {
                var adminCount = await _userRoleService.CountUsersInRoleAsync(AppRoles.Admin);
                if (adminCount <= 1)
                {
                    return BadRequest(new { message = "At least one admin account must remain in the workspace." });
                }
            }

            await _userRoleService.SetRoleAsync(user, normalizedRole);
            await _unitOfWork.SaveChangesAsync();

            var receiptCount = await _unitOfWork.Receipts.Query().CountAsync(receipt => receipt.UserId == user.Id);
            var budgetCount = await _unitOfWork.Budgets.Query().CountAsync(budget => budget.UserId == user.Id);
            var categoryCount = await _unitOfWork.Categories.Query().CountAsync(category => category.UserId == user.Id);
            var latestReceiptAt = await _unitOfWork.Receipts.Query()
                .Where(receipt => receipt.UserId == user.Id)
                .MaxAsync(receipt => (DateTime?)receipt.UploadedAt);

            return Ok(BuildUserSummary(user, receiptCount, budgetCount, categoryCount, latestReceiptAt));
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
