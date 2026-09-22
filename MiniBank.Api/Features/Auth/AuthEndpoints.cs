using Microsoft.AspNetCore.Http.HttpResults;
using MiniBank.Api.Infrastructure.Http;

namespace MiniBank.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group
            .MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Register a customer and issue an access token.")
            .ProducesValidationProblem()
            .ProducesProblems(ApiProblems.DuplicateEmail, ApiProblems.InternalServerError);

        return group;
    }

    private static async Task<
        Results<Ok<AuthResponse>, ValidationProblem, ProblemHttpResult>
    > RegisterAsync(
        RegisterRequest request,
        AuthService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.RegisterAsync(request, cancellationToken);

        return result.ToHttpResult();
    }
}
