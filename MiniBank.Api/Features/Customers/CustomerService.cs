using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using Npgsql;

namespace MiniBank.Api.Features.Customers;

public sealed class CustomerService(
    AppDbContext dbContext,
    CustomerRegistrationService registrationService
)
{
    public Task<List<CustomerResponse>> GetCustomersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Customers.AsNoTracking()
            .OrderBy(customer => customer.CreatedAt)
            .ThenBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CustomerResponse.ListProjection)
            .ToListAsync(cancellationToken);

    public Task<CustomerResponse?> GetCustomerByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Customers.AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(CustomerResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CustomerWriteResult> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await registrationService.RegisterAsync(request, cancellationToken);
        return new(
            result.Status,
            result.Customer is null ? null : CustomerResponse.FromEntity(result.Customer),
            result.Errors
        );
    }

    public async Task<CustomerWriteResult> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken
    )
    {
        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);
        if (customer is null)
        {
            return new(CustomerWriteStatus.NotFound);
        }

        var email = request.Email.Trim();
        if (await EmailExistsAsync(email, id, cancellationToken))
        {
            return new(CustomerWriteStatus.DuplicateEmail);
        }

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.TaxFileNumber = NormalizeOptionalValue(request.TaxFileNumber);
        customer.PhoneNumber = request.PhoneNumber.Trim();
        customer.CustomerStatus = request.CustomerStatus!.Value;

        return await SaveCustomerAsync(customer, cancellationToken);
    }

    public async Task<CustomerDeleteResult> DeleteCustomerAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var affectedRows = await dbContext
                .Customers.Where(customer => customer.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            return affectedRows == 0 ? CustomerDeleteResult.NotFound : CustomerDeleteResult.Deleted;
        }
        catch (PostgresException exception)
            when (exception.SqlState
                    is PostgresErrorCodes.ForeignKeyViolation
                        or PostgresErrorCodes.RestrictViolation
            )
        {
            return CustomerDeleteResult.HasAccounts;
        }
    }

    private Task<bool> EmailExistsAsync(
        string email,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken
    ) =>
        dbContext.Customers.AnyAsync(
            customer =>
                customer.Email == email
                && (!excludedCustomerId.HasValue || customer.Id != excludedCustomerId.Value),
            cancellationToken
        );

    private async Task<CustomerWriteResult> SaveCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(CustomerWriteStatus.Success, CustomerResponse.FromEntity(customer));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CustomerWriteStatus.NotFound);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Customers_Email",
            }
            )
        {
            // The unique index also protects concurrent requests that passed the earlier check.
            return new(CustomerWriteStatus.DuplicateEmail);
        }
    }

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
