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
}
