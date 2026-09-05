using System.ComponentModel.DataAnnotations;
using MiniBank.Api.Enums;

namespace MiniBank.Api.DTOs.Customers;

public record UpdateCustomerRequest(
    [Required] [StringLength(50)] string FirstName,
    [Required] [StringLength(100)] string LastName,
    [Required] [EmailAddress] string Email,
    [StringLength(10)] string TaxFileNumber,
    [Required] [Phone] string PhoneNumber,
    [Required] CustomerStatus CustomerStatus
);
