using MiniBank.Api.Features.Accounts;
using MiniBank.Api.Features.Customers;
using MiniBank.Api.Features.Transfers;

namespace MiniBank.Api.Infrastructure;

internal static class ApiServiceExtensions
{
    internal static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CustomerRegistrationService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<AccountService>();
        services.AddScoped<TransferService>();

        return services;
    }
}
