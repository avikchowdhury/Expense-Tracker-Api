namespace ExpenseTracker.Api.Services
{
    public sealed class FileStorageOptions
    {
        public string RootPath { get; set; } = "storage";
        public string AvatarsFolder { get; set; } = "avatars";
        public string ReceiptsFolder { get; set; } = "receipts";
    }

    public sealed class FileStoragePaths
    {
        public string RootPath { get; init; } = string.Empty;
        public string AvatarsPath { get; init; } = string.Empty;
        public string ReceiptsPath { get; init; } = string.Empty;
    }
}
