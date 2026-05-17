using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Api.Services;

public interface IAvatarStorageService
{
    Task<string> SaveAvatarAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
}
