using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniBank.Api.Features.Accounts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransactionRequest
{
    [Range(
        typeof(decimal),
        "0",
        "999999999999",
        MinimumIsExclusive = true,
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Amount must be greater than zero and no more than 999999999999."
    )]
    public required decimal Amount { get; init; }

    [MaxLength(255)]
    public string? Description { get; init; }
}
