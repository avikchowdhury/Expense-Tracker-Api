using ExpenseTracker.Api.Security;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Api.Dtos;

namespace ExpenseTracker.Api.Controllers
{
    [Route("api/profile")]
    [AppAuthorize]
    public class ProfileController : AppControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }


        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _profileService.GetProfileAsync(CurrentUserId, RequestUser.PrimaryRole, Request);
            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPost("avatar")]
        [RequestSizeLimit(5_000_000)] // 5MB limit
        public async Task<IActionResult> UploadAvatar([FromForm] Dtos.AvatarUploadDto dto)
        {
            var validationProblem = ValidateRequest(dto);
            if (validationProblem is not null)
                return validationProblem;

            var profile = await _profileService.UploadAvatarAsync(CurrentUserId, dto, RequestUser.PrimaryRole, Request);
            return profile == null ? NotFound() : Ok(new { profile.AvatarUrl });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var validationProblem = ValidateRequest(dto);
            if (validationProblem is not null)
                return validationProblem;

            var profile = await _profileService.UpdateProfileAsync(CurrentUserId, dto, RequestUser.PrimaryRole, Request);
            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var validationProblem = ValidateRequest(dto);
            if (validationProblem is not null)
                return validationProblem;

            if (!await _profileService.ChangePasswordAsync(CurrentUserId, dto))
                return NotFound();

            return Ok();
        }
    }
}
