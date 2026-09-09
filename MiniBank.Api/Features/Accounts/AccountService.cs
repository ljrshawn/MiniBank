using System.ComponentModel.DataAnnotations;
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

    public Task<TransactionResult> DepositAsync(
        Guid id,
        TransactionRequest request,
        CancellationToken cancellationToken
    ) => RecordTransactionAsync(id, request, TransactionType.Deposit, cancellationToken);

    public Task<TransactionResult> WithdrawAsync(
        Guid id,
        TransactionRequest request,
        CancellationToken cancellationToken
    ) => RecordTransactionAsync(id, request, TransactionType.Withdrawal, cancellationToken);

    public async Task<List<TransactionResponse>?> GetAccountTransactionsAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var accountExists = await dbContext.Accounts.AnyAsync(
            account => account.Id == id,
            cancellationToken
        );

        if (!accountExists)
        {
            return null;
        }

        return await dbContext
            .BankTransactions.AsNoTracking()
            .Where(transaction => transaction.AccountId == id)
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.Id)
            .Select(TransactionResponse.Projection)
            .ToListAsync(cancellationToken);
    }

    private async Task<TransactionResult> RecordTransactionAsync(
        Guid id,
        TransactionRequest request,
        TransactionType transactionType,
        CancellationToken cancellationToken
    )
    {
        var validationResults = new List<ValidationResult>();
        if (
            !Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true
            )
        )
        {
            return new TransactionResult(
                TransactionStatus.InvalidRequest,
                ErrorMessage: string.Join(
                    " ",
                    validationResults.Select(result => result.ErrorMessage)
                )
            );
        }

        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            account => account.Id == id,
            cancellationToken
        );

        if (account is null)
        {
            return new TransactionResult(TransactionStatus.NotFound);
        }

        if (transactionType == TransactionType.Withdrawal && account.Balance < request.Amount)
        {
            return new TransactionResult(TransactionStatus.InsufficientFunds);
        }

        if (
            transactionType == TransactionType.Deposit
            && account.Balance > decimal.MaxValue - request.Amount
        )
        {
            return new TransactionResult(TransactionStatus.BalanceLimitExceeded);
        }

        account.Balance +=
            transactionType == TransactionType.Deposit ? request.Amount : -request.Amount;

        var timestamp = timeProvider.GetUtcNow().UtcDateTime;

        var transaction = new BankTransaction
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            Amount = request.Amount,
            TransactionType = transactionType,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? transactionType.ToString()
                : request.Description.Trim(),
            BalanceAfterTransaction = account.Balance,
            TransactionDate = timestamp,
            CreatedAt = timestamp,
        };

        dbContext.BankTransactions.Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(transaction).State = EntityState.Detached;
            dbContext.Entry(account).State = EntityState.Detached;
            return new TransactionResult(TransactionStatus.Conflict);
        }

        return new TransactionResult(
            TransactionStatus.Success,
            TransactionResponse.FromEntity(transaction)
        );
    }

    private static string GenerateAccountNumber() =>
        RandomNumberGenerator
            .GetInt32(100_000_000, 1_000_000_000)
            .ToString(CultureInfo.InvariantCulture);

    private static bool IsAccountNumberCollision(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 } sqlite
        && sqlite.Message.Contains("Accounts.AccountNumber", StringComparison.Ordinal);
}
