using MiniBank.Api.Features.Accounts;
using MiniBank.Api.Features.Auth;
using MiniBank.Api.Features.Customers;
using MiniBank.Api.Features.Transfers;

namespace MiniBank.Api.Infrastructure;

internal static class ApiEndpointExtensions
{
    internal static RouteGroupBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapCustomerEndpoints();
        api.MapAccountEndpoints();
        api.MapTransferEndpoints();
        api.MapAuthEndpoints();

        return api;
    }
}
