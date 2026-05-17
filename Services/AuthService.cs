using System.Net.Mail;
using System.Security.Cryptography;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Shared.Constants;
using ExpenseTracker.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace ExpenseTracker.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IUserRoleService _userRoleService;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IWebHostEnvironment _environment;

    public AuthService(
        IUnitOfWork unitOfWork,
        IJwtService jwtService,
        IUserRoleService userRoleService,
        IMemoryCache cache,
        IEmailService emailService,
        IPasswordHashService passwordHashService,
        IWebHostEnvironment environment)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _userRoleService = userRoleService;
        _cache = cache;
        _emailService = emailService;
        _passwordHashService = passwordHashService;
        _environment = environment;
    }

    public async Task<AuthResponseDto> RegisterAsync(AuthRegisterDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        await EnsureEmailIsAvailableAsync(email, cancellationToken);

        var user = new User
        {
            Email = email,
            PasswordHash = string.Empty
        };
        user.PasswordHash = _passwordHashService.HashPassword(user, request.Password);

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _userRoleService.SetRoleAsync(user, AppRoles.User, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
    }

    public async Task<AuthResponseDto> LoginAsync(AuthLoginDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null)
        {
            throw new ApiRequestException(StatusCodes.Status401Unauthorized, ApplicationText.Auth.InvalidCredentials);
        }

        var verificationResult = _passwordHashService.VerifyPassword(
            user,
            user.PasswordHash,
            request.Password,
            out var upgradedHash);
        if (verificationResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            throw new ApiRequestException(StatusCodes.Status401Unauthorized, ApplicationText.Auth.InvalidCredentials);
        }

        if (!string.IsNullOrWhiteSpace(upgradedHash))
        {
            user.PasswordHash = upgradedHash;
        }

        var primaryRole = await _userRoleService.GetPrimaryRoleAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, primaryRole);
    }

    public async Task<SendOtpResponseDto> SendOtpAsync(OtpEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        await EnsureEmailIsAvailableAsync(email, cancellationToken);

        var otp = GenerateNumericCode();
        _cache.Set(
            $"{ApplicationText.CacheKeys.OtpPrefix}{email}",
            otp,
            TimeSpan.FromMinutes(ApplicationText.Auth.OtpExpiryMinutes));

        var emailed = await _emailService.SendOtpEmailAsync(email, otp, cancellationToken);
        if (emailed)
        {
            return new SendOtpResponseDto
            {
                Message = ApplicationText.Auth.OtpSentToEmail,
                DeliveryMode = ApplicationText.DeliveryModes.Email
            };
        }

        System.Diagnostics.Debug.WriteLine($"OTP for {email}: {otp}");

        return new SendOtpResponseDto
        {
            Message = _environment.IsDevelopment()
                ? ApplicationText.Auth.SmtpNotConfiguredOtpDevelopment
                : ApplicationText.Auth.OtpGeneratedWithoutEmail,
            DeliveryMode = _environment.IsDevelopment()
                ? ApplicationText.DeliveryModes.Development
                : ApplicationText.DeliveryModes.Email,
            DevelopmentOtp = _environment.IsDevelopment() ? otp : null
        };
    }

    public async Task<AuthResponseDto> RegisterWithOtpAsync(AuthRegisterOtpDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        await EnsureEmailIsAvailableAsync(email, cancellationToken);

        if (!_cache.TryGetValue($"{ApplicationText.CacheKeys.OtpPrefix}{email}", out string? cachedOtp) ||
            cachedOtp != request.Otp)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.InvalidOrExpiredOtp);
        }

        var user = new User
        {
            Email = email,
            PasswordHash = string.Empty
        };
        user.PasswordHash = _passwordHashService.HashPassword(user, request.Password);

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _userRoleService.SetRoleAsync(user, AppRoles.User, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cache.Remove($"{ApplicationText.CacheKeys.OtpPrefix}{email}");

        return _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
    }

    public async Task<MessageResponseDto> ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null)
        {
            return new MessageResponseDto
            {
                Message = ApplicationText.Auth.IfEmailExistsResetCodeSent
            };
        }

        var token = GenerateNumericCode();
        _cache.Set(
            $"{ApplicationText.CacheKeys.ResetPrefix}{email}",
            token,
            TimeSpan.FromMinutes(ApplicationText.Auth.ResetCodeExpiryMinutes));

        var emailed = await _emailService.SendPasswordResetEmailAsync(email, token, cancellationToken);
        if (!emailed && _environment.IsDevelopment())
        {
            System.Diagnostics.Debug.WriteLine($"Password reset OTP for {email}: {token}");
            return new MessageResponseDto
            {
                Message = ApplicationText.Auth.SmtpNotConfiguredResetDevelopment,
                DevelopmentToken = token
            };
        }

        return new MessageResponseDto
        {
            Message = ApplicationText.Auth.IfEmailExistsResetCodeSent
        };
    }

    public async Task<MessageResponseDto> ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeAndValidateEmail(request.Email);
        if (!_cache.TryGetValue($"{ApplicationText.CacheKeys.ResetPrefix}{email}", out string? cachedToken) ||
            cachedToken != request.Token)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.InvalidOrExpiredResetCode);
        }

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.InvalidOrExpiredResetCode);
        }

        user.PasswordHash = _passwordHashService.HashPassword(user, request.NewPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cache.Remove($"{ApplicationText.CacheKeys.ResetPrefix}{email}");

        return new MessageResponseDto
        {
            Message = ApplicationText.Auth.PasswordUpdatedSuccessfully
        };
    }

    public async Task<AuthResponseDto> BootstrapAdminAsync(int currentUserId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.FindAsync(currentUserId);
        if (user is null)
        {
            throw new ApiRequestException(StatusCodes.Status404NotFound, ApplicationText.Auth.UserNotFound);
        }

        var primaryRole = await _userRoleService.GetPrimaryRoleAsync(user, cancellationToken);
        if (string.Equals(primaryRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, AppRoles.Admin);
        }

        var adminExists = await _userRoleService.AnyUserInRoleAsync(AppRoles.Admin, cancellationToken);
        if (adminExists)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.AdminAlreadyExists);
        }

        await _userRoleService.SetRoleAsync(user, AppRoles.Admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, AppRoles.Admin);
    }

    private async Task EnsureEmailIsAvailableAsync(string email, CancellationToken cancellationToken)
    {
        var exists = await _unitOfWork.Users.Query()
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (exists)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.EmailAlreadyInUse);
        }
    }

    private static string GenerateNumericCode()
    {
        return RandomNumberGenerator
            .GetInt32(ApplicationText.Auth.OtpCodeMinValue, ApplicationText.Auth.OtpCodeMaxValueExclusive)
            .ToString();
    }

    private static string NormalizeAndValidateEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!IsValidEmail(normalized))
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.EnterValidEmailAddress);
        }

        return normalized;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email;
        }
        catch
        {
            return false;
        }
    }
}
