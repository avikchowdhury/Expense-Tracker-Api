namespace ExpenseTracker.Shared.Exceptions;

public sealed class ApiRequestException : Exception
{
    public ApiRequestException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
