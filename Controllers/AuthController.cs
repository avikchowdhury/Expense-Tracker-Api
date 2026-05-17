using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : AppControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRegisterDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthLoginDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] OtpEmailRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.SendOtpAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("register-otp")]
        public async Task<IActionResult> RegisterWithOtp([FromBody] AuthRegisterOtpDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.RegisterWithOtpAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request, CancellationToken cancellationToken)
        {
            var result = await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(result);
        }

        [AppAuthorize]
        [HttpPost("bootstrap-admin")]
        public async Task<IActionResult> BootstrapAdmin(CancellationToken cancellationToken)
        {
            var result = await _authService.BootstrapAdminAsync(CurrentUserId, cancellationToken);
            return Ok(result);
        }
    }
}
