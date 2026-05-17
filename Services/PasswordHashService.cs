using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Services;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyPassword(
        User user,
        string hashedPassword,
        string providedPassword,
        out string? upgradedHash)
    {
        upgradedHash = null;
        var result = _passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            upgradedHash = HashPassword(user, providedPassword);
            return result;
        }

        if (result == PasswordVerificationResult.Success)
        {
            return result;
        }

        if (!VerifyLegacySha256(providedPassword, hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        upgradedHash = HashPassword(user, providedPassword);
        return PasswordVerificationResult.SuccessRehashNeeded;
    }

    private static bool VerifyLegacySha256(string providedPassword, string hashedPassword)
    {
        return string.Equals(HashLegacySha256(providedPassword), hashedPassword, StringComparison.Ordinal);
    }

    private static string HashLegacySha256(string password)
    {
        using var sha = SHA256.Create();
        var hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashed);
    }
}
