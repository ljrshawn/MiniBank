using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Customers;

public sealed class CustomerService(
    AppDbContext dbContext,
    IPasswordHasher<Customer> passwordHasher,
    TimeProvider timeProvider
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
            .Select(CustomerResponse.Projection)
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
        var email = request.Email.Trim();
        if (await EmailExistsAsync(email, null, cancellationToken))
        {
            return new(CustomerWriteStatus.DuplicateEmail);
        }

        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            TaxFileNumber = NormalizeOptionalValue(request.TaxFileNumber),
            PhoneNumber = request.PhoneNumber.Trim(),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        customer.PasswordHash = passwordHasher.HashPassword(customer, request.Password);
        dbContext.Customers.Add(customer);

        return await SaveCustomerAsync(customer, cancellationToken);
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
        catch (SqliteException exception)
            when (exception.SqliteExtendedErrorCode == 1811
                || exception.SqliteExtendedErrorCode == 787
            )
        {
            // SQLite reports RESTRICT as a trigger constraint (1811), or a FK constraint (787).
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
                    is SqliteException { SqliteExtendedErrorCode: 2067 } sqlite
                && sqlite.Message.Contains("Customers.Email", StringComparison.Ordinal)
            )
        {
            // The unique index also protects concurrent requests that passed the earlier check.
            return new(CustomerWriteStatus.DuplicateEmail);
        }
    }

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
