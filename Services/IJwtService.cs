using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services
{
    public interface IJwtService
    {
        string BuildToken(int userId, string email, string role);
        AuthResponseDto GenerateRefreshTokenResponse(int userId, string email, string role);
    }
}
