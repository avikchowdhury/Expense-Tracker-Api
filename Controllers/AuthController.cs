using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : AppControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly IUserRoleService _userRoleService;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;

        public AuthController(
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            IUserRoleService userRoleService,
            IMemoryCache cache,
            IEmailService emailService,
            IWebHostEnvironment environment)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _userRoleService = userRoleService;
            _cache = cache;
            _emailService = emailService;
            _environment = environment;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRegisterDto request)
        {
            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
            {
                return BadRequest(new { message = "Enter a valid email address." });
            }

            if (await _unitOfWork.Users.Query().AnyAsync(u => u.Email == email))
            {
                return BadRequest(new { message = "Email already in use" });
            }

            var user = new User
            {
                Email = email,
                PasswordHash = HashPassword(request.Password)
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _userRoleService.SetRoleAsync(user, AppRoles.User);
            await _unitOfWork.SaveChangesAsync();

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
            return Ok(tokenResponse);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthLoginDto request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Email == email);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            var primaryRole = await _userRoleService.GetPrimaryRoleAsync(user);
            await _unitOfWork.SaveChangesAsync();
            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, primaryRole);
            return Ok(tokenResponse);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] OtpEmailRequest request, CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Enter a valid email address." });
            if (await _unitOfWork.Users.Query().AnyAsync(u => u.Email == email))
                return BadRequest(new { message = "Email already in use" });
            var otp = new Random().Next(100000, 999999).ToString();
            _cache.Set($"otp_{email}", otp, TimeSpan.FromMinutes(10));

            var emailed = await _emailService.SendOtpEmailAsync(email, otp, cancellationToken);
            if (emailed)
            {
                return Ok(new SendOtpResponseDto
                {
                    Message = "OTP sent to your email.",
                    DeliveryMode = "email"
                });
            }

            System.Diagnostics.Debug.WriteLine($"OTP for {email}: {otp}");

            return Ok(new SendOtpResponseDto
            {
                Message = _environment.IsDevelopment()
                    ? "SMTP is not configured, so the development OTP is returned below."
                    : "OTP generated, but email delivery is not configured on the server.",
                DeliveryMode = _environment.IsDevelopment() ? "development" : "email",
                DevelopmentOtp = _environment.IsDevelopment() ? otp : null
            });
        }

        [HttpPost("register-otp")]
        public async Task<IActionResult> RegisterWithOtp([FromBody] AuthRegisterOtpDto request)
        {
            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Enter a valid email address." });
            if (await _unitOfWork.Users.Query().AnyAsync(u => u.Email == email))
                return BadRequest(new { message = "Email already in use" });
            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Password is required." });
            if (!_cache.TryGetValue($"otp_{email}", out string? cachedOtp) || cachedOtp != request.Otp)
                return BadRequest(new { message = "Invalid or expired OTP." });
            var user = new User
            {
                Email = email,
                PasswordHash = HashPassword(request.Password)
            };
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _userRoleService.SetRoleAsync(user, AppRoles.User);
            await _unitOfWork.SaveChangesAsync();
            _cache.Remove($"otp_{email}");
            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
            return Ok(tokenResponse);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request, CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Enter a valid email address." });

            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Email == email);
            // Always return OK to avoid user enumeration
            if (user == null)
                return Ok(new { message = "If that email exists, a reset code has been sent." });

            var token = new Random().Next(100000, 999999).ToString();
            _cache.Set($"reset_{email}", token, TimeSpan.FromMinutes(15));

            var emailed = await _emailService.SendPasswordResetEmailAsync(email, token, cancellationToken);
            if (!emailed && _environment.IsDevelopment())
            {
                System.Diagnostics.Debug.WriteLine($"Password reset OTP for {email}: {token}");
                return Ok(new
                {
                    message = "SMTP not configured. Development reset code returned.",
                    developmentToken = token
                });
            }

            return Ok(new { message = "If that email exists, a reset code has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Enter a valid email address." });

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { message = "Password must be at least 6 characters." });

            if (!_cache.TryGetValue($"reset_{email}", out string? cachedToken) || cachedToken != request.Token)
                return BadRequest(new { message = "Invalid or expired reset code." });

            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return BadRequest(new { message = "Invalid or expired reset code." });

            user.PasswordHash = HashPassword(request.NewPassword);
            await _unitOfWork.SaveChangesAsync();
            _cache.Remove($"reset_{email}");

            return Ok(new { message = "Password updated successfully. You can now log in." });
        }

        [AppAuthorize]
        [HttpPost("bootstrap-admin")]
        public async Task<IActionResult> BootstrapAdmin()
        {
            var user = await _unitOfWork.Users.FindAsync(CurrentUserId);
            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            var primaryRole = await _userRoleService.GetPrimaryRoleAsync(user);
            if (string.Equals(primaryRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(_jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, AppRoles.Admin));
            }

            var adminExists = await _userRoleService.AnyUserInRoleAsync(AppRoles.Admin);
            if (adminExists)
            {
                return BadRequest(new { message = "An admin account already exists. Ask an admin to grant access." });
            }

            await _userRoleService.SetRoleAsync(user, AppRoles.Admin);
            await _unitOfWork.SaveChangesAsync();

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, AppRoles.Admin);
            return Ok(tokenResponse);
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashed);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
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

        public class OtpEmailRequest
        {
            public string Email { get; set; } = string.Empty;
        }
    }
}
