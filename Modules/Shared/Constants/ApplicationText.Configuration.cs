namespace ExpenseTracker.Shared.Constants;

public static partial class ApplicationText
{
    public static class Configuration
    {
        public const string DefaultConnectionName = "DefaultConnection";
        public const string StorageSection = "Storage";
        public const string JwtSettingsSection = "JwtSettings";
        public const string EmailSection = "Email";
        public const string OpenAiApiKeyKey = "OpenAI:ApiKey";
        public const string OpenAiModelKey = "OpenAI:Model";
        public const string OpenAiResponsesEndpointKey = "OpenAI:ResponsesEndpoint";
        public const string AzureAiEndpointKey = "AzureAI:Endpoint";
        public const string AzureAiKeyKey = "AzureAI:Key";
    }

    public static class Policies
    {
        public const string AllowLocalhost = "AllowLocalhost";

        public static readonly string[] AllowedOrigins =
        [
            "http://localhost:4200",
            "https://localhost:4200"
        ];
    }

    public static class Swagger
    {
        public const string Endpoint = "/swagger/v1/swagger.json";
        public const string Title = "ExpenseTracker API v1";
    }
}
