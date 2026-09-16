using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using MiniBank.Api.Features.Common;

namespace MiniBank.Api.Features.Transfers;

public sealed class TransferService(AppDbContext dbContext, TimeProvider timeProvider)
{
    public Task<TransferResponse?> GetTransferByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Transfers.AsNoTracking()
            .Where(transfer => transfer.Id == id)
            .Select(TransferResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<TransferResult> CreateTransferAsync(
        CreateTransferRequest request,
        CancellationToken cancellationToken
    )
    {
        var validationError = RequestValidation.GetError(request);
        if (validationError is not null)
        {
            return new TransferResult(
                TransferResultStatus.InvalidRequest,
                ErrorMessage: validationError
            );
        }

        var accounts = await dbContext
            .Accounts.Where(account =>
                account.Id == request.FromAccountId || account.Id == request.ToAccountId
            )
            .ToListAsync(cancellationToken);
        var fromAccount = accounts.SingleOrDefault(account => account.Id == request.FromAccountId);
        var toAccount = accounts.SingleOrDefault(account => account.Id == request.ToAccountId);

        if (fromAccount is null || toAccount is null)
        {
            return new TransferResult(
                TransferResultStatus.NotFound,
                ErrorMessage: fromAccount is null
                    ? $"No source account found with ID {request.FromAccountId}."
                    : $"No destination account found with ID {request.ToAccountId}."
            );
        }

        if (fromAccount.Balance < request.Amount)
        {
            return new TransferResult(TransferResultStatus.InsufficientFunds);
        }

        if (toAccount.Balance > MoneyLimits.MaximumBalance - request.Amount)
        {
            return new TransferResult(TransferResultStatus.BalanceLimitExceeded);
        }

        fromAccount.Balance -= request.Amount;
        toAccount.Balance += request.Amount;
        var timestamp = timeProvider.GetUtcNow().UtcDateTime;
        var transfer = new Transfer
        {
            Id = Guid.CreateVersion7(),
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            TransferStatus = TransferStatus.Completed,
            Amount = request.Amount,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? "Transfer"
                : request.Description.Trim(),
            TransactionDate = timestamp,
            CreatedAt = timestamp,
        };
        var outgoingTransaction = BankTransaction.Create(
            fromAccount,
            TransactionType.TransferOut,
            request.Amount,
            request.Description,
            timestamp
        );
        var incomingTransaction = BankTransaction.Create(
            toAccount,
            TransactionType.TransferIn,
            request.Amount,
            request.Description,
            timestamp
        );

        dbContext.Transfers.Add(transfer);
        dbContext.BankTransactions.AddRange(outgoingTransaction, incomingTransaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(transfer).State = EntityState.Detached;
            dbContext.Entry(outgoingTransaction).State = EntityState.Detached;
            dbContext.Entry(incomingTransaction).State = EntityState.Detached;
            dbContext.Entry(fromAccount).State = EntityState.Detached;
            dbContext.Entry(toAccount).State = EntityState.Detached;
            return new TransferResult(TransferResultStatus.Conflict);
        }

        return new TransferResult(
            TransferResultStatus.Success,
            TransferResponse.FromEntity(transfer)
        );
    }
}
