namespace ExpenseTracker.Api.Security
{
    public static class AppRoles
    {
        public const string User = "User";
        public const string Admin = "Admin";

        public static readonly string[] All = [User, Admin];
    }
}
