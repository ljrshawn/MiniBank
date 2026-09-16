using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MiniBank.Api.Features.Common;

namespace MiniBank.Api.Features.Accounts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransactionRequest
{
    [MoneyAmount]
    public required decimal Amount { get; init; }

    [MaxLength(255)]
    public string? Description { get; init; }
}
