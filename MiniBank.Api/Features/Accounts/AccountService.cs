using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Accounts;

public sealed class AccountService(AppDbContext dbContext, TimeProvider timeProvider)
{
    private const int MaxAccountNumberAttempts = 5;

    public async Task<AccountResponse?> CreateAccountAsync(
        Guid customerId,
        CreateAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        var customerExists = await dbContext.Customers.AnyAsync(
            customer => customer.Id == customerId,
            cancellationToken
        );

        if (!customerExists)
        {
            return null;
        }

        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            AccountNumber = GenerateAccountNumber(),
            Balance = 0m,
            AccountType = request.AccountType!.Value,
            AccountStatus = AccountStatus.Active,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Accounts.Add(account);

        for (var attempt = 1; attempt <= MaxAccountNumberAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return AccountResponse.FromEntity(account);
            }
            catch (DbUpdateException exception) when (IsAccountNumberCollision(exception))
            {
                // A failed insert remains Added in EF. Reuse it with a fresh number.
                if (attempt < MaxAccountNumberAttempts)
                {
                    account.AccountNumber = GenerateAccountNumber();
                }
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 787 })
            {
                // The customer may have been deleted after the existence check.
                dbContext.Entry(account).State = EntityState.Detached;
                return null;
            }
        }

        dbContext.Entry(account).State = EntityState.Detached;
        throw new AccountNumberAllocationException();
    }

    public Task<AccountResponse?> GetAccountByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Accounts.AsNoTracking()
            .Where(account => account.Id == id)
            .Select(AccountResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

    private static string GenerateAccountNumber() =>
        RandomNumberGenerator
            .GetInt32(100_000_000, 1_000_000_000)
            .ToString(CultureInfo.InvariantCulture);

    private static bool IsAccountNumberCollision(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 } sqlite
        && sqlite.Message.Contains("Accounts.AccountNumber", StringComparison.Ordinal);
}
