using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Entities;
using MiniBank.Api.Features.Common;
using Npgsql;

namespace MiniBank.Api.Features.Customers;

public sealed class CustomerRegistrationService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider
)
{
    public async Task<CustomerRegistrationResult> RegisterAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = RequestValidation.GetErrors(request);
        if (errors.Count > 0)
        {
            return new(CustomerWriteStatus.InvalidRequest, Errors: errors);
        }

        var email = request.Email.Trim();
        if (await dbContext.Customers.AnyAsync(customer => customer.Email == email, cancellationToken))
        {
            return new(CustomerWriteStatus.DuplicateEmail);
        }

        var timestamp = timeProvider.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedAt = timestamp,
        };
        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            TaxFileNumber = string.IsNullOrWhiteSpace(request.TaxFileNumber)
                ? null
                : request.TaxFileNumber.Trim(),
            CreatedAt = timestamp,
            UserId = user.Id,
            User = user,
        };
        user.Customer = customer;

        try
        {
            // Identity adds the entire graph, so EF saves user and customer atomically.
            cancellationToken.ThrowIfCancellationRequested();
            var result = await userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                return new(CustomerWriteStatus.Success, customer);
            }

            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return new(CustomerWriteStatus.DuplicateEmail);
            }

            return new(
                CustomerWriteStatus.InvalidRequest,
                Errors: result.Errors
                    .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal)
                        ? nameof(request.Password)
                        : nameof(request.Email))
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())
            );
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            return new(CustomerWriteStatus.DuplicateEmail);
        }
        finally
        {
            // Failed inserts must not be persisted by a later save in the same scope.
            if (dbContext.Entry(user).State == EntityState.Added)
            {
                dbContext.Entry(customer).State = EntityState.Detached;
                dbContext.Entry(user).State = EntityState.Detached;
            }
        }
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "EmailIndex" or "UserNameIndex" or "IX_Customers_Email",
        };
}
