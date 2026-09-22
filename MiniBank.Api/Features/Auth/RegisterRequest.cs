using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.Features.Auth;

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;
}
