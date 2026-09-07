using System.Linq.Expressions;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;

namespace MiniBank.Api.Features.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    CustomerStatus CustomerStatus
)
{
    internal static readonly Expression<Func<Customer, CustomerResponse>> Projection =
        customer => new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            customer.CustomerStatus
        );

    internal static CustomerResponse FromEntity(Customer customer) => Map(customer);

    private static readonly Func<Customer, CustomerResponse> Map = Projection.Compile();
}
