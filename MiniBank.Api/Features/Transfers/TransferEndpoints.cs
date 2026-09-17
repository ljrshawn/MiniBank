using Microsoft.AspNetCore.Http.HttpResults;
using MiniBank.Api.Infrastructure.Http;

namespace MiniBank.Api.Features.Transfers;

public static class TransferEndpoints
{
    private const string GetTransferRouteName = "GetTransferById";

    public static RouteGroupBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/transfers").WithTags("Transfers");

        group
            .MapGet("/{id:guid}", GetTransferByIdAsync)
            .WithName(GetTransferRouteName)
            .WithSummary("Get a transfer by ID.")
            .ProducesProblems(ApiProblems.TransferNotFound());

        group
            .MapPost("/", CreateTransferAsync)
            .WithName("CreateTransfer")
            .WithSummary("Transfer money between two existing accounts.")
            .ProducesValidationProblem()
            .ProducesProblems(
                ApiProblems.InvalidTransfer,
                ApiProblems.AccountNotFound(),
                ApiProblems.TransferConflict
            );

        return group;
    }

    private static async Task<Results<Ok<TransferResponse>, ProblemHttpResult>> GetTransferByIdAsync(
        Guid id,
        TransferService service,
        CancellationToken cancellationToken
    )
    {
        var transfer = await service.GetTransferByIdAsync(id, cancellationToken);
        return transfer.ToHttpResult(
            ApiProblems.TransferNotFound(id),
            value => TypedResults.Ok(value)
        );
    }

    private static async Task<
        Results<CreatedAtRoute<TransferResponse>, ProblemHttpResult>
    > CreateTransferAsync(
        CreateTransferRequest request,
        TransferService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateTransferAsync(request, cancellationToken);
        return result.ToHttpResult(transfer =>
            TypedResults.CreatedAtRoute(transfer, GetTransferRouteName, new { id = transfer.Id })
        );
    }
}
