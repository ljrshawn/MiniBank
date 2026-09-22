using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.Features.Auth;

public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
