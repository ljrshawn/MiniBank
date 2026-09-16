using MiniBank.Api.Enums;

namespace MiniBank.Api.Entities;

public sealed class BankTransaction
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required TransactionType TransactionType { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfterTransaction { get; set; }

    public DateTime TransactionDate { get; set; }

    public required string Description { get; set; }

    public DateTime CreatedAt { get; set; }

    internal static BankTransaction Create(
        Account account,
        TransactionType transactionType,
        decimal amount,
        string? description,
        DateTime timestamp
    ) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            TransactionType = transactionType,
            Amount = amount,
            BalanceAfterTransaction = account.Balance,
            Description = string.IsNullOrWhiteSpace(description)
                ? transactionType.ToString()
                : description.Trim(),
            TransactionDate = timestamp,
            CreatedAt = timestamp,
        };
}
