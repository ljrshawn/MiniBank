using System.ComponentModel.DataAnnotations;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Customers;

public sealed record UpdateCustomerRequest
{
    [Required, StringLength(50)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [StringLength(10)]
    public string? TaxFileNumber { get; init; }

    [Required, Phone, StringLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, EnumDataType(typeof(CustomerStatus))]
    public CustomerStatus? CustomerStatus { get; init; }
}
