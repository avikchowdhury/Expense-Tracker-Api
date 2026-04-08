namespace ExpenseTracker.Api.Dtos
{
    public class UpdateProfileDto
    {
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }
}