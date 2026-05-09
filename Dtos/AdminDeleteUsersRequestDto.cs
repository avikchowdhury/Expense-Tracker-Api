namespace ExpenseTracker.Api.Dtos
{
    public sealed class AdminDeleteUsersRequestDto
    {
        public IReadOnlyCollection<int> UserIds { get; set; } = Array.Empty<int>();
    }
}
