using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.DTOs.Customers;

public record CreateCustomerRequest(
    [Required] [StringLength(50)] string FirstName,
    [Required] [StringLength(100)] string LastName,
    [Required] [EmailAddress] string Email,
    [Required] [StringLength(100)] string PassWord,
    [StringLength(10)] string TaxFileNumber,
    [Required] [Phone] string PhoneNumber
);
