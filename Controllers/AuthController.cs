using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Mail;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ExpenseTrackerDbContext _dbContext;
        private readonly IJwtService _jwtService;
        private readonly IMemoryCache _cache;

        public AuthController(ExpenseTrackerDbContext dbContext, IJwtService jwtService, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _jwtService = jwtService;
            _cache = cache;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRegisterDto request)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { message = "Email already in use" });
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password)
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
            return Ok(tokenResponse);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthLoginDto request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Email);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            var tokenResponse = _jwtService.GenerateRefreshTokenResponse(user.Id, user.Email, user.Role);
            return Ok(tokenResponse);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] AuthRegisterDto request)
        {
            if (!Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
                return BadRequest(new { message = "Only gmail.com emails are allowed." });
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email already in use" });
            var otp = new Random().Next(100000, 999999).ToString();
            _cache.Set($"otp_{request.Email}", otp, TimeSpan.FromMinutes(10));
            // TODO: Replace with real email sending
            System.Diagnostics.Debug.WriteLine($"OTP for {request.Email}: {otp}");
            // Optionally, use SmtpClient to send email here
            return Ok(new { message = "OTP sent to your email." });
        }

        [HttpPost("register-otp")]
        public async Task<IActionResult> RegisterWithOtp([FromBody] AuthRegisterOtpDto request)
        {
            if (!Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
                return BadRequest(new { message = "Only gmail.com emails are allowed." });
            if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email already in use" });
            if (!_cache.TryGetValue($"otp_{request.Email}", out string? cachedOtp) || cachedOtp != request.Otp)
                return BadRequest(new { message = "Invalid or expired OTP." });
            var user = new User
            {
                Email = request.Email,
                PasswordHash = HashPassword("TempPasswordChangeMe") // Password to be set after OTP
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _cache.Remove($"otp_{request.Email}");
            return Ok(new { message = "Email verified. Please set your password." });
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
    }
}
