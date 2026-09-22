using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.Features.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1_440)]
    public int ExpirationMinutes { get; set; } = 60;
}
