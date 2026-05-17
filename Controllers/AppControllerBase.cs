using ExpenseTracker.Shared.Constants;
using ExpenseTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ExpenseTracker.Api.Controllers
{
    [ApiController]
    public abstract class AppControllerBase : ControllerBase
    {
        protected RequestUserContext RequestUser =>
            HttpContext.GetRequestUserContext()
            ?? BuildRequestUserFromClaims();

        protected int CurrentUserId => RequestUser.UserId;

        private RequestUserContext BuildRequestUserFromClaims()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException(ApplicationText.Security.AuthenticatedUserContextUnavailable);
            }

            var email = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? string.Empty;
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (roles.Length == 0)
            {
                roles = [AppRoles.User];
            }

            return new RequestUserContext(userId, email, roles);
        }

        protected IActionResult? ValidateRequest<TRequest>(TRequest? request)
        {
            if (request is null)
            {
                ModelState.AddModelError(string.Empty, ApplicationText.Validation.RequestBodyRequired);
                return ValidationProblem(ModelState);
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            if (Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
            {
                return null;
            }

            foreach (var validationResult in validationResults)
            {
                var memberNames = validationResult.MemberNames.Any()
                    ? validationResult.MemberNames
                    : [string.Empty];

                foreach (var memberName in memberNames)
                {
                    ModelState.AddModelError(
                        memberName,
                        validationResult.ErrorMessage ?? ApplicationText.Validation.InvalidRequest);
                }
            }

            return ValidationProblem(ModelState);
        }
    }
}
