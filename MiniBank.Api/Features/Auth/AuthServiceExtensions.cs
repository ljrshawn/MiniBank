using System.Text;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Auth;

internal static class AuthServiceExtensions
{
    internal static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Key)
                    && Encoding.UTF8.GetByteCount(options.Key) >= 32,
                "Jwt:Key must contain at least 32 UTF-8 bytes for HMAC-SHA256."
            )
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<AuthService>();
        services.AddScoped<JwtTokenService>();
        return services;
    }
}
