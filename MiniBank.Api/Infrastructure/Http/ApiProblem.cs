using Microsoft.AspNetCore.Http.HttpResults;

namespace MiniBank.Api.Infrastructure.Http;

internal sealed record ApiProblem(int StatusCode, string? Title = null, string? Detail = null)
{
    internal ProblemHttpResult ToResult() =>
        TypedResults.Problem(statusCode: StatusCode, title: Title, detail: Detail);
}
