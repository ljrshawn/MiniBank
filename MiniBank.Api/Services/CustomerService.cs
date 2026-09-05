using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.DTOs.Customers;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Services;

public class CustomerService
{
    private readonly AppDbContext _dbContext;

    public CustomerService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request)
    {
        Customer customer = new()
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PassWord = request.PassWord,
            TaxFileNumber = request.TaxFileNumber,
            PhoneNumber = request.PhoneNumber,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        return new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt
        );
    }

    public async Task<List<CustomerResponse>> GetAllCustomersAsync()
    {
        return await _dbContext
            .Customers.Select(c => new CustomerResponse(
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email,
                c.PhoneNumber,
                c.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<CustomerResponse?> GetCustomerByIdAsync(Guid id)
    {
        var customer = await _dbContext.Customers.FindAsync(id);
        if (customer is null)
            return null;

        return new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt
        );
    }

    public async Task<CustomerResponse?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var customer = await _dbContext.Customers.FindAsync(id);
        if (customer is null)
            return null;

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;
        customer.TaxFileNumber = request.TaxFileNumber;
        customer.PhoneNumber = request.PhoneNumber;
        customer.CustomerStatus = request.CustomerStatus;

        await _dbContext.SaveChangesAsync();

        return new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt
        );
    }

    public async Task<bool> DeleteCustomerAsync(Guid id)
    {
        await _dbContext.Customers.Where(c => c.Id == id).ExecuteDeleteAsync();
        return true;
    }
}
