using System.Linq.Expressions;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Accounts;

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    TransactionType TransactionType,
    decimal Amount,
    decimal BalanceAfterTransaction,
    DateTime TransactionDate,
    string Description,
    DateTime CreatedAt
)
{
    internal static readonly Expression<Func<BankTransaction, TransactionResponse>> Projection =
        transaction => new TransactionResponse(
            transaction.Id,
            transaction.AccountId,
            transaction.TransactionType,
            transaction.Amount,
            transaction.BalanceAfterTransaction,
            transaction.TransactionDate,
            transaction.Description,
            transaction.CreatedAt
        );

    internal static TransactionResponse FromEntity(BankTransaction transaction) => Map(transaction);

    private static readonly Func<BankTransaction, TransactionResponse> Map = Projection.Compile();
}
