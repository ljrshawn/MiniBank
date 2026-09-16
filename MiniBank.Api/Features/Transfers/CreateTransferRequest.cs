using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MiniBank.Api.Features.Common;

namespace MiniBank.Api.Features.Transfers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTransferRequest : IValidatableObject
{
    public required Guid FromAccountId { get; init; }

    public required Guid ToAccountId { get; init; }

    [MoneyAmount]
    public required decimal Amount { get; init; }

    [MaxLength(255)]
    public string? Description { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromAccountId == Guid.Empty || ToAccountId == Guid.Empty)
        {
            yield return new ValidationResult(
                "Source and destination account IDs must not be empty.",
                [nameof(FromAccountId), nameof(ToAccountId)]
            );
        }

        if (FromAccountId == ToAccountId)
        {
            yield return new ValidationResult(
                "Source and destination accounts must be different.",
                [nameof(ToAccountId)]
            );
        }
    }
}
