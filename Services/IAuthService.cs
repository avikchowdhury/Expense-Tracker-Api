using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(AuthRegisterDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(AuthLoginDto request, CancellationToken cancellationToken = default);
    Task<SendOtpResponseDto> SendOtpAsync(OtpEmailRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RegisterWithOtpAsync(AuthRegisterOtpDto request, CancellationToken cancellationToken = default);
    Task<MessageResponseDto> ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default);
    Task<MessageResponseDto> ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> BootstrapAdminAsync(int currentUserId, CancellationToken cancellationToken = default);
}
