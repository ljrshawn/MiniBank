using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.Features.Common;

internal static class RequestValidation
{
    internal static string? GetError(object request)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true
        )
            ? null
            : string.Join(" ", results.Select(result => result.ErrorMessage));
    }
}
