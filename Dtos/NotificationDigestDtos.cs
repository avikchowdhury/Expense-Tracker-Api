namespace ExpenseTracker.Api.Dtos
{
    public class NotificationDeliveryStatusDto
    {
        public bool IsOperational { get; set; }
        public string DeliveryMode { get; set; } = "file-preview";
        public string Message { get; set; } = string.Empty;
    }

    public class SendTestDigestRequestDto
    {
        public string Type { get; set; } = string.Empty;
    }

    public class SendTestDigestResultDto
    {
        public string Type { get; set; } = string.Empty;
        public bool Delivered { get; set; }
        public string DeliveryMode { get; set; } = "file-preview";
        public string Message { get; set; } = string.Empty;
        public string? PreviewPath { get; set; }
    }
}
