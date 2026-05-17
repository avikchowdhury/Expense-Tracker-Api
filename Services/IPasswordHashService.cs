using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Services;

public interface IPasswordHashService
{
    string HashPassword(User user, string password);

    PasswordVerificationResult VerifyPassword(
        User user,
        string hashedPassword,
        string providedPassword,
        out string? upgradedHash);
}
