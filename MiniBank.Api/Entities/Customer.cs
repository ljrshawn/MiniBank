using MiniBank.Api.Enums;

namespace MiniBank.Api.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? TaxFileNumber { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public CustomerStatus CustomerStatus { get; set; } = CustomerStatus.Active;

    public DateTime CreatedAt { get; set; }

    public List<Account> Accounts { get; set; } = [];
}
