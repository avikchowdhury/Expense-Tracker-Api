using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Security
{
    public static class HttpContextRequestUserExtensions
    {
        private const string RequestUserContextKey = "__RequestUserContext";

        public static void SetRequestUserContext(
            this HttpContext httpContext,
            RequestUserContext requestUserContext)
        {
            httpContext.Items[RequestUserContextKey] = requestUserContext;
        }

        public static RequestUserContext? GetRequestUserContext(this HttpContext httpContext)
        {
            return httpContext.Items.TryGetValue(RequestUserContextKey, out var value)
                ? value as RequestUserContext
                : null;
        }
    }
}
