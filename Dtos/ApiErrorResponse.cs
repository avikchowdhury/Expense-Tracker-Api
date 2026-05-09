namespace ExpenseTracker.Api.Dtos
{
    public sealed class ApiErrorResponse
    {
        public int StatusCode { get; set; }

        public string Message { get; set; } = string.Empty;

        public string TraceId { get; set; } = string.Empty;
    }
}
