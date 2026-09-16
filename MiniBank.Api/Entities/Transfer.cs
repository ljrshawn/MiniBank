using MiniBank.Api.Enums;

namespace MiniBank.Api.Entities;

public sealed class Transfer
{
    public Guid Id { get; set; }

    public Guid FromAccountId { get; set; }

    public Account FromAccount { get; set; } = null!;

    public Guid ToAccountId { get; set; }

    public Account ToAccount { get; set; } = null!;

    public required TransferStatus TransferStatus { get; set; }

    public decimal Amount { get; set; }

    public DateTime TransactionDate { get; set; }

    public required string Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
