namespace ExpenseTracker.Api.Configuration
{
    public sealed class JwtSettings
    {
        public string Secret { get; set; } = "supersecretkey1234567890";
        public string Issuer { get; set; } = "ExpenseTracker";
        public string Audience { get; set; } = "ExpenseTrackerUsers";
        public int ExpiryMinutes { get; set; } = 60;
    }
}
