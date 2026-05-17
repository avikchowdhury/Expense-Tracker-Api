using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Services;

public sealed class AvatarStorageService : IAvatarStorageService
{
    private readonly FileStoragePaths _storagePaths;

    public AvatarStorageService(FileStoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public async Task<string> SaveAvatarAsync(int userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storagePaths.AvatarsPath);

        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension;
        var fileName = $"{userId}{safeExtension}";
        var path = Path.Combine(_storagePaths.AvatarsPath, fileName);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        return $"{ApplicationText.Storage.AvatarRequestPath}/{fileName}";
    }
}
