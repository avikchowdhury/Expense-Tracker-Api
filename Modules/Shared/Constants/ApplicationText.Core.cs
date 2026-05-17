namespace ExpenseTracker.Shared.Constants;

public static partial class ApplicationText
{
    public static class Storage
    {
        public const string RootFolder = "storage";
        public const string AvatarsFolder = "avatars";
        public const string ReceiptsFolder = "receipts";
        public const string NotificationPreviewFolder = "notification-previews";
        public const string AvatarRequestPath = "/avatars";
        public const string EmptyJsonObject = "{}";
        public const string QuickAddFilePrefix = "quick-add";
        public const string QuickAddSourceJson = "{\"source\":\"quick-add\"}";
    }

    public static class Defaults
    {
        public const string UnknownVendor = "Unknown";
        public const string GeneralCategory = "General";
        public const string UncategorizedCategory = "Uncategorized";
        public const string UsdCurrency = "USD";
        public const string NotAvailable = "N/A";
    }

    public static class CacheKeys
    {
        public const string OtpPrefix = "otp_";
        public const string ResetPrefix = "reset_";
    }

    public static class DeliveryModes
    {
        public const string Email = "email";
        public const string Development = "development";
        public const string Smtp = "smtp";
        public const string FilePreview = "file-preview";
    }

    public static class Severity
    {
        public const string Info = "info";
        public const string Positive = "positive";
        public const string Warning = "warning";
        public const string Critical = "critical";
    }
}
