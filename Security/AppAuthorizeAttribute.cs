namespace ExpenseTracker.Api.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AppAuthorizeAttribute : Attribute
    {
        public AppAuthorizeAttribute(params string[] roles)
        {
            if (roles.Length > 0)
            {
                Roles = string.Join(",", roles);
            }
        }

        public string? Roles { get; set; }
    }
}
