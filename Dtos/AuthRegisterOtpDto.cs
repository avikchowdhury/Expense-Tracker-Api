namespace ExpenseTracker.Api.Dtos
{
    public class AuthRegisterOtpDto
    {
        public string Email { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
}