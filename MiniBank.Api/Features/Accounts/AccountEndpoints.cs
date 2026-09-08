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

    private static ProblemHttpResult AccountNotFound(Guid id)
    {
        return TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Account not found.",
            detail: $"No account found with ID {id}."
        );
    }
}
