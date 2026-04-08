using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
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
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;

        public AuthController(
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            IMemoryCache cache,
            IEmailService emailService,
            IWebHostEnvironment environment)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
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

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, NormalizeRole(user.Role));
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

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, NormalizeRole(user.Role));
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
            _cache.Remove($"otp_{email}");
            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, NormalizeRole(user.Role));
            return Ok(tokenResponse);
        }

        [Authorize]
        [HttpPost("bootstrap-admin")]
        public async Task<IActionResult> BootstrapAdmin()
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _unitOfWork.Users.FindAsync(userId);
            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(_jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, "Admin"));
            }

            var adminExists = await _unitOfWork.Users.Query().AnyAsync(existingUser => existingUser.Role == "Admin");
            if (adminExists)
            {
                return BadRequest(new { message = "An admin account already exists. Ask an admin to grant access." });
            }

            user.Role = "Admin";
            await _unitOfWork.SaveChangesAsync();

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, "Admin");
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

        private static string NormalizeRole(string? role)
        {
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "User";
        }

        public class OtpEmailRequest
        {
            public string Email { get; set; } = string.Empty;
        }
    }
}
