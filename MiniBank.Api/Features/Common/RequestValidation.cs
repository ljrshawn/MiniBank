using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.Features.Common;

internal static class RequestValidation
{
    internal static Dictionary<string, string[]> GetErrors(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true
        );

        return results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => new { Member = member, Message = result.ErrorMessage ?? "Invalid value." }))
            .GroupBy(error => error.Member)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
    }

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
