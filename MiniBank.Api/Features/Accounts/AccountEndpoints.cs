using Microsoft.AspNetCore.Http.HttpResults;
using MiniBank.Api.Infrastructure.Http;

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
            .ProducesProblems(ApiProblems.CustomerNotFound, ApiProblems.AccountNumberUnavailable);

        group
            .MapGet("/{id:guid}", GetAccountByIdAsync)
            .WithName(GetAccountRouteName)
            .WithSummary("Get an account by ID.")
            .ProducesProblems(ApiProblems.AccountNotFound());

        group
            .MapPost("/{id:guid}/deposit", DepositToAccountAsync)
            .WithName("DepositToAccount")
            .WithSummary("Deposit money into an existing account.")
            .ProducesValidationProblem()
            .ProducesProblems(
                ApiProblems.AccountNotFound(),
                ApiProblems.DepositBalanceLimitExceeded,
                ApiProblems.TransactionConflict
            );

        group
            .MapPost("/{id:guid}/withdraw", WithdrawFromAccountAsync)
            .WithName("WithdrawFromAccount")
            .WithSummary("Withdraw money from an existing account.")
            .ProducesValidationProblem()
            .ProducesProblems(
                ApiProblems.AccountNotFound(),
                ApiProblems.WithdrawalInsufficientFunds,
                ApiProblems.TransactionConflict
            );

        group
            .MapGet("/{id:guid}/transactions", GetAccountTransactionsAsync)
            .WithName("GetAccountTransactions")
            .WithSummary("Get all transactions for an existing account.")
            .ProducesProblems(ApiProblems.AccountNotFound());

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
        var account = await service.CreateAccountAsync(customerId, request, cancellationToken);
        return account.ToHttpResult(
            ApiProblems.CustomerNotFound,
            value => TypedResults.CreatedAtRoute(value, GetAccountRouteName, new { id = value.Id })
        );
    }

    private static async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> GetAccountByIdAsync(
        Guid id,
        AccountService service,
        CancellationToken cancellationToken
    )
    {
        var account = await service.GetAccountByIdAsync(id, cancellationToken);

        return account.ToHttpResult(ApiProblems.AccountNotFound(id), value => TypedResults.Ok(value));
    }

    private static async Task<
        Results<Ok<TransactionResponse>, ProblemHttpResult>
    > DepositToAccountAsync(
        Guid id,
        TransactionRequest request,
        AccountService service,
        CancellationToken cancellationToken
    ) =>
        (await service.DepositAsync(id, request, cancellationToken))
            .ToHttpResult(id, transaction => TypedResults.Ok(transaction));

    private static async Task<
        Results<Ok<TransactionResponse>, ProblemHttpResult>
    > WithdrawFromAccountAsync(
        Guid id,
        TransactionRequest request,
        AccountService service,
        CancellationToken cancellationToken
    ) =>
        (await service.WithdrawAsync(id, request, cancellationToken))
            .ToHttpResult(id, transaction => TypedResults.Ok(transaction));

    private static async Task<
        Results<Ok<List<TransactionResponse>>, ProblemHttpResult>
    > GetAccountTransactionsAsync(
        Guid id,
        AccountService service,
        CancellationToken cancellationToken
    )
    {
        var transactions = await service.GetAccountTransactionsAsync(id, cancellationToken);
        return transactions.ToHttpResult(
            ApiProblems.AccountNotFound(id),
            value => TypedResults.Ok(value)
        );
    }
}
