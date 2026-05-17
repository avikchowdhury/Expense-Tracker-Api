using Microsoft.AspNetCore.Http;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotBlankAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NonEmptyFileAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is IFormFile file && file.Length > 0;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyCollectionAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string)
        {
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var _ in enumerable)
            {
                return true;
            }
        }

        return false;
    }
}
