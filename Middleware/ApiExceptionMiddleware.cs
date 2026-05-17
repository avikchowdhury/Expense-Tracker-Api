using System.Text.Json;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Shared.Exceptions;

namespace ExpenseTracker.Api.Middleware
{
    public sealed class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(
            RequestDelegate next,
            ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled exception for request {Path}", context.Request.Path);
                await WriteErrorResponseAsync(context, exception);
            }
        }

        private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                ApiRequestException apiRequestException => (apiRequestException.StatusCode, apiRequestException.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication is required for this resource."),
                ArgumentException argumentException => (StatusCodes.Status400BadRequest, argumentException.Message),
                BadHttpRequestException badHttpRequestException => (badHttpRequestException.StatusCode, badHttpRequestException.Message),
                _ => (StatusCodes.Status500InternalServerError, "Something went wrong while processing the request.")
            };

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
