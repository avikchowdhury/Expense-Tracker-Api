namespace ExpenseTracker.Api.Configuration
{
    public sealed class JwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = "ExpenseTracker";
        public string Audience { get; set; } = "ExpenseTrackerUsers";
        public int ExpiryMinutes { get; set; } = 60;
    }
}
