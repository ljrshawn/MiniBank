using Microsoft.AspNetCore.Http.HttpResults;

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
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/", CreateTransferAsync)
            .WithName("CreateTransfer")
            .WithSummary("Transfer money between two existing accounts.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Ok<TransferResponse>, ProblemHttpResult>> GetTransferByIdAsync(
        Guid id,
        TransferService service,
        CancellationToken cancellationToken
    )
    {
        var transfer = await service.GetTransferByIdAsync(id, cancellationToken);
        return transfer is not null
            ? TypedResults.Ok(transfer)
            : TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Transfer not found",
                detail: $"No transfer found with ID {id}."
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
        return result.Status switch
        {
            TransferResultStatus.Success => TypedResults.CreatedAtRoute(
                result.Transfer!,
                GetTransferRouteName,
                new { id = result.Transfer!.Id }
            ),
            TransferResultStatus.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Account not found",
                detail: result.ErrorMessage
            ),
            TransferResultStatus.InvalidRequest => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid transfer",
                detail: result.ErrorMessage
            ),
            TransferResultStatus.InsufficientFunds => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Insufficient funds",
                detail: "The source account balance is too low for this transfer."
            ),
            TransferResultStatus.BalanceLimitExceeded => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Balance limit exceeded",
                detail: "The transfer would exceed the maximum supported destination account balance."
            ),
            TransferResultStatus.Conflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account changed",
                detail: "An account changed while processing this transfer. Please try again."
            ),
            _ => throw new InvalidOperationException(
                $"Unknown transfer result status: {result.Status}."
            ),
        };
    }
}
