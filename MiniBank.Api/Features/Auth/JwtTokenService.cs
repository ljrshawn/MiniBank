using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public AuthResponse CreateToken(ApplicationUser user)
    {
        var email = user.Email
            ?? throw new InvalidOperationException("A user must have an email to receive a token.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        ];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new AuthResponse(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn: checked(_options.ExpirationMinutes * 60)
        );
    }
}
