using System.Linq.Expressions;
using System.Text.Json.Serialization;
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AccountResponse>? Accounts
)
{
    internal static readonly Expression<Func<Customer, CustomerResponse>> Projection =
        CreateProjection(includeAccounts: true);

    internal static readonly Expression<Func<Customer, CustomerResponse>> ListProjection =
        CreateProjection(includeAccounts: false);

    private static Expression<Func<Customer, CustomerResponse>> CreateProjection(
        bool includeAccounts
    ) =>
        customer => new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt,
            customer.CustomerStatus,
            includeAccounts
                ? customer
                    .Accounts.AsQueryable()
                    .OrderBy(account => account.CreatedAt)
                    .ThenBy(account => account.Id)
                    .Select(AccountResponse.Projection)
                    .ToList()
                : null
        );

    internal static CustomerResponse FromEntity(Customer customer) => Map(customer);

    private static readonly Func<Customer, CustomerResponse> Map = Projection.Compile();
}
