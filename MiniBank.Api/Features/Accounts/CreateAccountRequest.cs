using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Accounts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAccountRequest
{
    [Required, EnumDataType(typeof(AccountType))]
    public AccountType? AccountType { get; init; }
}
