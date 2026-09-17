using Microsoft.AspNetCore.Http.HttpResults;
using MiniBank.Api.Features.Accounts;
using MiniBank.Api.Features.Customers;
using MiniBank.Api.Features.Transfers;

namespace MiniBank.Api.Infrastructure.Http;

// Services return business outcomes. This HTTP layer decides how to expose failures.
internal static class ApiResultExtensions
{
    internal static Results<TSuccess, ProblemHttpResult> ToHttpResult<TValue, TSuccess>(
        this TValue? value,
        ApiProblem whenMissing,
        Func<TValue, TSuccess> onSuccess
    )
        where TValue : class
        where TSuccess : IResult =>
        value is null ? whenMissing.ToResult() : onSuccess(value);

    internal static Results<TSuccess, ProblemHttpResult> ToHttpResult<TSuccess>(
        this CustomerWriteResult result,
        Func<CustomerResponse, TSuccess> onSuccess
    )
        where TSuccess : IResult =>
        result.Status switch
        {
            CustomerWriteStatus.Success => onSuccess(RequireValue(result.Customer)),
            CustomerWriteStatus.NotFound => ApiProblems.CustomerNotFound.ToResult(),
            CustomerWriteStatus.DuplicateEmail => ApiProblems.DuplicateEmail.ToResult(),
            _ => throw new InvalidOperationException(
                $"Unknown customer write status: {result.Status}."
            ),
        };

    internal static Results<NoContent, ProblemHttpResult> ToHttpResult(
        this CustomerDeleteResult result
    ) =>
        result switch
        {
            CustomerDeleteResult.Deleted => TypedResults.NoContent(),
            CustomerDeleteResult.NotFound => ApiProblems.CustomerNotFound.ToResult(),
            CustomerDeleteResult.HasAccounts => ApiProblems.CustomerHasAccounts.ToResult(),
            _ => throw new InvalidOperationException($"Unknown customer delete result: {result}."),
        };

    internal static Results<TSuccess, ProblemHttpResult> ToHttpResult<TSuccess>(
        this TransactionResult result,
        Guid accountId,
        Func<TransactionResponse, TSuccess> onSuccess
    )
        where TSuccess : IResult =>
        result.Status switch
        {
            TransactionStatus.Success => onSuccess(RequireValue(result.Transaction)),
            TransactionStatus.NotFound => ApiProblems.AccountNotFound(accountId).ToResult(),
            TransactionStatus.InvalidRequest =>
                (ApiProblems.InvalidTransaction with { Detail = result.ErrorMessage }).ToResult(),
            TransactionStatus.InsufficientFunds => ApiProblems.WithdrawalInsufficientFunds.ToResult(),
            TransactionStatus.BalanceLimitExceeded =>
                ApiProblems.DepositBalanceLimitExceeded.ToResult(),
            TransactionStatus.Conflict => ApiProblems.TransactionConflict.ToResult(),
            _ => throw new InvalidOperationException($"Unknown transaction status: {result.Status}."),
        };

    internal static Results<TSuccess, ProblemHttpResult> ToHttpResult<TSuccess>(
        this TransferResult result,
        Func<TransferResponse, TSuccess> onSuccess
    )
        where TSuccess : IResult =>
        result.Status switch
        {
            TransferResultStatus.Success => onSuccess(RequireValue(result.Transfer)),
            TransferResultStatus.NotFound =>
                (ApiProblems.AccountNotFound() with { Detail = result.ErrorMessage }).ToResult(),
            TransferResultStatus.InvalidRequest =>
                (ApiProblems.InvalidTransfer with { Detail = result.ErrorMessage }).ToResult(),
            TransferResultStatus.InsufficientFunds => ApiProblems.TransferInsufficientFunds.ToResult(),
            TransferResultStatus.BalanceLimitExceeded =>
                ApiProblems.TransferBalanceLimitExceeded.ToResult(),
            TransferResultStatus.Conflict => ApiProblems.TransferConflict.ToResult(),
            _ => throw new InvalidOperationException(
                $"Unknown transfer result status: {result.Status}."
            ),
        };

    private static T RequireValue<T>(T? value)
        where T : class =>
        value ?? throw new InvalidOperationException("A successful result must contain a value.");
}
