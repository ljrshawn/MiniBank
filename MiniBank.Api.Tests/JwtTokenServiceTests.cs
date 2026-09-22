using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MiniBank.Api.Entities;
using MiniBank.Api.Features.Auth;

namespace MiniBank.Api.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Token_lifetime_uses_one_clock_read_and_reports_seconds()
    {
        var clock = new AdvancingTimeProvider();
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            Key = MiniBankApiFactory.SigningKey,
            ExpirationMinutes = 15,
        }), clock);

        var response = service.CreateToken(new ApplicationUser { Email = "david@example.com" });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(TimeSpan.FromMinutes(15), token.ValidTo - token.ValidFrom);
        Assert.Equal(1, clock.ReadCount);
    }

    [Theory]
    [InlineData("Key", "")]
    [InlineData("Key", "too-short")]
    [InlineData("Issuer", "")]
    [InlineData("Audience", "")]
    [InlineData("ExpirationMinutes", "0")]
    [InlineData("ExpirationMinutes", "1441")]
    public async Task Invalid_jwt_options_fail_at_startup(string setting, string value)
    {
        await using var factory = new MiniBankApiFactory();
        await using var configured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting($"Jwt:{setting}", value));
        Assert.Throws<OptionsValidationException>(() => configured.CreateClient());
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        internal int ReadCount { get; private set; }

        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero).AddSeconds(ReadCount++);
    }
}
