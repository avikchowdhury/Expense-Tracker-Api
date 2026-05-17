using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class AvatarUploadDto
    {
        [NonEmptyFile(ErrorMessage = ApplicationText.Profile.AvatarFileRequired)]
        public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
    }
}
