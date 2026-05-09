using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    public abstract class AppControllerBase : ControllerBase
    {
        protected RequestUserContext RequestUser =>
            HttpContext.GetRequestUserContext()
            ?? BuildRequestUserFromClaims();

        protected int CurrentUserId => RequestUser.UserId;

        private RequestUserContext BuildRequestUserFromClaims()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("Authenticated user context is not available.");
            }

            var email = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? string.Empty;
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (roles.Length == 0)
            {
                roles = [AppRoles.User];
            }

            return new RequestUserContext(userId, email, roles);
        }
    }
}
