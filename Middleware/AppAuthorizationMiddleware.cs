using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace ExpenseTracker.Api.Middleware
{
    public sealed class AppAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;

        public AppAuthorizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null)
            {
                await _next(context);
                return;
            }

            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await _next(context);
                return;
            }

            var authorizeMetadata = endpoint.Metadata.GetOrderedMetadata<AppAuthorizeAttribute>();
            if (authorizeMetadata.Count == 0)
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await WriteErrorAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    ApplicationText.Security.AuthenticationRequired);
                return;
            }

            var requestUser = context.GetRequestUserContext();
            if (requestUser is null)
            {
                await WriteErrorAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    ApplicationText.Security.AuthenticatedUserContextUnavailable);
                return;
            }

            foreach (var authorizeData in authorizeMetadata)
            {
                var requiredRoles = (authorizeData.Roles ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (requiredRoles.Length > 0 && !requiredRoles.Any(requestUser.IsInRole))
                {
                    await WriteErrorAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        ApplicationText.Security.AccessDenied);
                    return;
                }
            }

            await _next(context);
        }

        private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var payload = new ApiErrorResponse
            {
                StatusCode = statusCode,
                Message = message,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
