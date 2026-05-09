namespace ExpenseTracker.Api.Security
{
    public sealed class RequestUserContext
    {
        public RequestUserContext(int userId, string email, IReadOnlyCollection<string> roles)
        {
            UserId = userId;
            Email = email;
            Roles = roles;
        }

        public int UserId { get; }

        public string Email { get; }

        public IReadOnlyCollection<string> Roles { get; }

        public string PrimaryRole =>
            Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase)
                ? AppRoles.Admin
                : Roles.FirstOrDefault() ?? AppRoles.User;

        public bool IsInRole(string role) =>
            Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
