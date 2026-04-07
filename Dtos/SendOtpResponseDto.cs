namespace ExpenseTracker.Api.Dtos
{
    public class SendOtpResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = "email";
        public string? DevelopmentOtp { get; set; }
    }
}
