using MiniBank.Api.Enums;

namespace MiniBank.Api.Entities;

public class Account
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public required string AccountNumber { get; set; }

    public decimal Balance { get; set; }

    public required AccountType AccountType { get; set; }

    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    public DateTime CreatedAt { get; set; }
}
