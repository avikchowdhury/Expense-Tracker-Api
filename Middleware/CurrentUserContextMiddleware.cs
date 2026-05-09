using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExpenseTracker.Api.Middleware
{
    public sealed class CurrentUserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentUserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ExpenseTrackerDbContext dbContext)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException();
            }

            var user = await dbContext.Users
                .AsNoTracking()
                .Include(existingUser => existingUser.RoleMappings)
                .ThenInclude(mapping => mapping.Role)
                .FirstOrDefaultAsync(existingUser => existingUser.Id == userId);

            if (user is null)
            {
                throw new UnauthorizedAccessException();
            }

            var roles = user.RoleMappings
                .Select(mapping => string.Equals(mapping.Role.Name, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                    ? AppRoles.Admin
                    : AppRoles.User)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (roles.Length == 0)
            {
                roles = [string.Equals(user.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                    ? AppRoles.Admin
                    : AppRoles.User];
            }

            context.SetRequestUserContext(new RequestUserContext(
                user.Id,
                user.Email,
                roles));

            await _next(context);
        }
    }
}
