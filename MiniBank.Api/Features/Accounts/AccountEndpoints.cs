using Microsoft.AspNetCore.Http.HttpResults;

namespace MiniBank.Api.Features.Accounts;

public static class AccountEndpoints
{
    private const string GetAccountRouteName = "GetAccountById";

    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/accounts").WithTags("Accounts");

        group
            .MapPost("/customers/{customerId:guid}", CreateAccountAsync)
            .WithName("CreateCustomerAccount")
            .WithSummary("Create an account with a zero balance for an existing customer.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group
            .MapGet("/{id:guid}", GetAccountByIdAsync)
            .WithName(GetAccountRouteName)
            .WithSummary("Get an account by ID.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/{id:guid}/deposit", DepositToAccountAsync)
            .WithName("DepositToAccount")
            .WithSummary("Deposit money into an existing account.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group
            .MapPost("/{id:guid}/withdraw", WithdrawFromAccountAsync)
            .WithName("WithdrawFromAccount")
            .WithSummary("Withdraw money from an existing account.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group
            .MapGet("/{id:guid}/transactions", GetAccountTransactionsAsync)
            .WithName("GetAccountTransactions")
            .WithSummary("Get all transactions for an existing account.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<
        Results<CreatedAtRoute<AccountResponse>, ProblemHttpResult>
    > CreateAccountAsync(
        Guid customerId,
        CreateAccountRequest request,
        AccountService service,
        CancellationToken cancellationToken
    )
    {
        AccountResponse? account;
        try
        {
            account = await service.CreateAccountAsync(customerId, request, cancellationToken);
        }
        catch (AccountNumberAllocationException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Account number unavailable",
                detail: "A unique account number could not be allocated. Please try again."
            );
        }

        if (account is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Customer not found",
                detail: "No customer exists with the supplied ID."
            );
        }

        return TypedResults.CreatedAtRoute(account, GetAccountRouteName, new { id = account.Id });
    }

    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> GetAccountByIdAsync(
        Guid id,
        AccountService service,
        CancellationToken cancellationToken
    )
    {
        var account = await service.GetAccountByIdAsync(id, cancellationToken);

        return account is null ? AccountNotFound(id) : TypedResults.Ok(account);
    }

    private static async Task<
        Results<Ok<TransactionResponse>, ProblemHttpResult>
    > DepositToAccountAsync(
        Guid id,
        TransactionRequest request,
        AccountService service,
        CancellationToken cancellationToken
    ) => ToTransactionHttpResult(id, await service.DepositAsync(id, request, cancellationToken));

    private static async Task<
        Results<Ok<TransactionResponse>, ProblemHttpResult>
    > WithdrawFromAccountAsync(
        Guid id,
        TransactionRequest request,
        AccountService service,
        CancellationToken cancellationToken
    ) => ToTransactionHttpResult(id, await service.WithdrawAsync(id, request, cancellationToken));

    private static async Task<
        Results<Ok<List<TransactionResponse>>, ProblemHttpResult>
    > GetAccountTransactionsAsync(
        Guid id,
        AccountService service,
        CancellationToken cancellationToken
    )
    {
        var transactions = await service.GetAccountTransactionsAsync(id, cancellationToken);
        return transactions is not null ? TypedResults.Ok(transactions) : AccountNotFound(id);
    }

    private static Results<Ok<TransactionResponse>, ProblemHttpResult> ToTransactionHttpResult(
        Guid id,
        TransactionResult result
    ) => result.Status switch
    {
        TransactionStatus.Success => TypedResults.Ok(result.Transaction!),
        TransactionStatus.NotFound => AccountNotFound(id),
        TransactionStatus.InvalidRequest => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid transaction",
            detail: result.ErrorMessage
        ),
        TransactionStatus.InsufficientFunds => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Insufficient funds",
            detail: "The account balance is too low for this withdrawal."
        ),
        TransactionStatus.BalanceLimitExceeded => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Balance limit exceeded",
            detail: "The deposit would exceed the maximum supported account balance."
        ),
        TransactionStatus.Conflict => TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Account changed",
            detail: "The account changed while processing this transaction. Please try again."
        ),
        _ => throw new InvalidOperationException($"Unknown transaction status: {result.Status}."),
    };

    private static ProblemHttpResult AccountNotFound(Guid id) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Account not found",
            detail: $"No account found with ID {id}."
        );
}
