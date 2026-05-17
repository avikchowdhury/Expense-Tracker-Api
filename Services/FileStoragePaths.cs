using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services
{
    public sealed class FileStorageOptions
    {
        public string RootPath { get; set; } = ApplicationText.Storage.RootFolder;
        public string AvatarsFolder { get; set; } = ApplicationText.Storage.AvatarsFolder;
        public string ReceiptsFolder { get; set; } = ApplicationText.Storage.ReceiptsFolder;
    }

    public sealed class FileStoragePaths
    {
        public string RootPath { get; init; } = string.Empty;
        public string AvatarsPath { get; init; } = string.Empty;
        public string ReceiptsPath { get; init; } = string.Empty;
        public string NotificationPreviewsPath { get; init; } = string.Empty;
    }
}
