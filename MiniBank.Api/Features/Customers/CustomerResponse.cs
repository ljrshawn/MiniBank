using System.Linq.Expressions;
using MiniBank.Api.Entities;
using MiniBank.Api.Enums;
using MiniBank.Api.Features.Accounts;

namespace MiniBank.Api.Features.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    CustomerStatus CustomerStatus,
    IReadOnlyList<AccountResponse> Accounts
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
            customer.CustomerStatus,
            customer
                .Accounts.AsQueryable()
                .OrderBy(account => account.CreatedAt)
                .ThenBy(account => account.Id)
                .Select(AccountResponse.Projection)
                .ToList()
        );

    internal static CustomerResponse FromEntity(Customer customer) => Map(customer);

    private static readonly Func<Customer, CustomerResponse> Map = Projection.Compile();
}
