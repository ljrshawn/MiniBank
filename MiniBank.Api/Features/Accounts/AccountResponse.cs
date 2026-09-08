using System.Linq.Expressions;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Accounts;

public sealed record AccountResponse(
    Guid Id,
    Guid CustomerId,
    string AccountNumber,
    decimal Balance,
    AccountType AccountType,
    AccountStatus AccountStatus,
    DateTime CreatedAt
)
{
    internal static readonly Expression<Func<Account, AccountResponse>> Projection =
        account => new AccountResponse(
            account.Id,
            account.CustomerId,
            account.AccountNumber,
            account.Balance,
            account.AccountType,
            account.AccountStatus,
            account.CreatedAt
        );

    internal static AccountResponse FromEntity(Account account) => Map(account);

    private static readonly Func<Account, AccountResponse> Map = Projection.Compile();
}
