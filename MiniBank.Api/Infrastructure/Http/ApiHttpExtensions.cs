using System.Diagnostics;

namespace MiniBank.Api.Infrastructure.Http;

internal static class ApiHttpExtensions
{
    internal static IServiceCollection AddApiHttp(this IServiceCollection services)
    {
        // .NET 10 installs validation filters for annotated endpoint parameters and DTOs.
        services.AddValidation();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                problem.Instance = context.HttpContext.Request.Path;
                problem.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

                if (problem is HttpValidationProblemDetails)
                {
                    problem.Title = ApiProblems.ValidationTitle;
                }
            }
        );
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        return services;
    }

    internal static IApplicationBuilder UseApiHttp(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(context =>
            ApiProblems.ForStatusCode(context.HttpContext.Response.StatusCode)
                .ToResult()
                .ExecuteAsync(context.HttpContext)
        );
        return app;
    }

    internal static RouteHandlerBuilder ProducesProblems(
        this RouteHandlerBuilder builder,
        params ApiProblem[] problems
    )
    {
        foreach (var statusCode in problems.Select(problem => problem.StatusCode).Distinct())
        {
            builder.ProducesProblem(statusCode);
        }

        return builder;
    }
}
