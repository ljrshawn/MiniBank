using Microsoft.AspNetCore.Diagnostics;
using MiniBank.Api.Features.Accounts;

namespace MiniBank.Api.Infrastructure.Http;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (
            exception is OperationCanceledException
            && httpContext.RequestAborted.IsCancellationRequested
        )
        {
            return false;
        }

        var problem = exception switch
        {
            AccountNumberAllocationException => ApiProblems.AccountNumberUnavailable,
            BadHttpRequestException badRequest => ApiProblems.ForStatusCode(badRequest.StatusCode),
            _ => ApiProblems.InternalServerError,
        };

        // .NET 10 suppresses default diagnostics for exceptions handled by IExceptionHandler.
        if (problem.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Request {Method} {Path} failed with status {StatusCode}.",
                httpContext.Request.Method,
                httpContext.Request.Path,
                problem.StatusCode
            );
        }

        await problem.ToResult().ExecuteAsync(httpContext);
        return true;
    }
}
