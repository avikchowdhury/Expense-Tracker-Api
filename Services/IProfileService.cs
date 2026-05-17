using ExpenseTracker.Api.Dtos;
using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Services;

public interface IProfileService
{
    Task<ProfileDto?> GetProfileAsync(int userId, string role, HttpRequest request, CancellationToken cancellationToken = default);
    Task<ProfileDto?> UploadAvatarAsync(int userId, AvatarUploadDto dto, string role, HttpRequest request, CancellationToken cancellationToken = default);
    Task<ProfileDto?> UpdateProfileAsync(int userId, UpdateProfileDto dto, string role, HttpRequest request, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
