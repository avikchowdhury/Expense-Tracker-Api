using System.Net.Mail;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Shared.Constants;
using ExpenseTracker.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class ProfileService : IProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAvatarStorageService _avatarStorageService;
    private readonly IPasswordHashService _passwordHashService;

    public ProfileService(
        IUnitOfWork unitOfWork,
        IAvatarStorageService avatarStorageService,
        IPasswordHashService passwordHashService)
    {
        _unitOfWork = unitOfWork;
        _avatarStorageService = avatarStorageService;
        _passwordHashService = passwordHashService;
    }

    public async Task<ProfileDto?> GetProfileAsync(int userId, string role, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user == null ? null : MapProfile(user, role, request);
    }

    public async Task<ProfileDto?> UploadAvatarAsync(int userId, AvatarUploadDto dto, string role, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.FindAsync(userId);
        if (user == null)
        {
            return null;
        }

        user.AvatarUrl = await _avatarStorageService.SaveAvatarAsync(userId, dto.File, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapProfile(user, role, request);
    }

    public async Task<ProfileDto?> UpdateProfileAsync(int userId, UpdateProfileDto dto, string role, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.FindAsync(userId);
        if (user == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var normalizedEmail = NormalizeAndValidateEmail(dto.Email);
            var emailTaken = await _unitOfWork.Users.Query()
                .AnyAsync(candidate => candidate.Id != userId && candidate.Email == normalizedEmail, cancellationToken);

            if (emailTaken)
            {
                throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.EmailAlreadyInUse);
            }

            user.Email = normalizedEmail;
        }

        if (dto.FullName is not null)
        {
            user.FullName = NormalizeOptionalText(dto.FullName);
        }

        if (dto.Address is not null)
        {
            user.Address = NormalizeOptionalText(dto.Address);
        }

        if (dto.Phone is not null)
        {
            var normalizedPhone = NormalizePhone(dto.Phone);
            if (normalizedPhone == null && !string.IsNullOrWhiteSpace(dto.Phone))
            {
                throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Profile.InvalidPhoneNumber);
            }

            user.Phone = normalizedPhone;
        }

        if (dto.BudgetNotificationsEnabled.HasValue)
            user.BudgetNotificationsEnabled = dto.BudgetNotificationsEnabled.Value;
        if (dto.AnomalyNotificationsEnabled.HasValue)
            user.AnomalyNotificationsEnabled = dto.AnomalyNotificationsEnabled.Value;
        if (dto.SubscriptionNotificationsEnabled.HasValue)
            user.SubscriptionNotificationsEnabled = dto.SubscriptionNotificationsEnabled.Value;
        if (dto.WeeklySummaryEmailEnabled.HasValue)
            user.WeeklySummaryEmailEnabled = dto.WeeklySummaryEmailEnabled.Value;
        if (dto.MonthlyReportEmailEnabled.HasValue)
            user.MonthlyReportEmailEnabled = dto.MonthlyReportEmailEnabled.Value;
        if (dto.WeeklySummaryDay is not null)
            user.WeeklySummaryDay = NormalizeWeeklySummaryDay(dto.WeeklySummaryDay);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapProfile(user, role, request);
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        var verificationResult = _passwordHashService.VerifyPassword(
            user,
            user.PasswordHash,
            dto.OldPassword,
            out _);

        if (verificationResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Profile.OldPasswordIncorrect);
        }

        user.PasswordHash = _passwordHashService.HashPassword(user, dto.NewPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeAndValidateEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();

        try
        {
            return new MailAddress(normalized).Address == normalized
                ? normalized
                : throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.EnterValidEmailAddress);
        }
        catch
        {
            throw new ApiRequestException(StatusCodes.Status400BadRequest, ApplicationText.Auth.EnterValidEmailAddress);
        }
    }

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var normalized = phone.Trim();
        var withoutPlus = normalized.StartsWith("+", StringComparison.Ordinal)
            ? normalized[1..]
            : normalized;

        if (withoutPlus.Any(character => !char.IsDigit(character)))
        {
            return null;
        }

        if (withoutPlus.Length < 11 || withoutPlus.Length > 14)
        {
            return null;
        }

        var countryCodeLength = withoutPlus.Length - 10;
        if (countryCodeLength < 1 || countryCodeLength > 4)
        {
            return null;
        }

        return $"+{withoutPlus}";
    }

    private static string NormalizeWeeklySummaryDay(string? day)
    {
        if (string.IsNullOrWhiteSpace(day))
        {
            return "Monday";
        }

        return day.Trim().ToLowerInvariant() switch
        {
            "monday" => "Monday",
            "tuesday" => "Tuesday",
            "wednesday" => "Wednesday",
            "thursday" => "Thursday",
            "friday" => "Friday",
            "saturday" => "Saturday",
            "sunday" => "Sunday",
            _ => "Monday"
        };
    }

    private static ProfileDto MapProfile(User user, string role, HttpRequest request)
    {
        return new ProfileDto
        {
            Email = user.Email,
            Role = role,
            AvatarUrl = BuildAvatarUrl(request, user.AvatarUrl),
            FullName = user.FullName,
            Phone = user.Phone,
            Address = user.Address,
            BudgetNotificationsEnabled = user.BudgetNotificationsEnabled,
            AnomalyNotificationsEnabled = user.AnomalyNotificationsEnabled,
            SubscriptionNotificationsEnabled = user.SubscriptionNotificationsEnabled,
            WeeklySummaryEmailEnabled = user.WeeklySummaryEmailEnabled,
            MonthlyReportEmailEnabled = user.MonthlyReportEmailEnabled,
            WeeklySummaryDay = user.WeeklySummaryDay
        };
    }

    private static string? BuildAvatarUrl(HttpRequest request, string? storedAvatarUrl)
    {
        if (string.IsNullOrWhiteSpace(storedAvatarUrl))
        {
            return null;
        }

        if (Uri.TryCreate(storedAvatarUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var normalizedPath = storedAvatarUrl.StartsWith("/", StringComparison.Ordinal)
            ? storedAvatarUrl
            : $"/{storedAvatarUrl}";

        return $"{request.Scheme}://{request.Host}{normalizedPath}";
    }
}
