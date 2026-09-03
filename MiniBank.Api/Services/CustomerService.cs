using MiniBank.Api.DTOs.Customers;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Services;

public class CustomerService
{
    // private readonly List<CustomerResponse> _customers = new();
    private static readonly List<CustomerResponse> _customers =
    [
        new(
            Guid.Parse("ec242485-df42-40e1-983f-a2448e7ed292"),
            "John",
            "Doe",
            "john.doe@example.com",
            "123-456-7890",
            DateTime.UtcNow
        ),
        new(
            Guid.Parse("da7acda3-453e-4917-8783-3df7fe873c67"),
            "Jane",
            "Smith",
            "jane.smith@example.com",
            "098-765-4321",
            DateTime.UtcNow
        ),
        new(
            Guid.Parse("743c5832-0b78-46c0-ad5e-4ccfdda97e47"),
            "Alice",
            "Johnson",
            "alice.johnson@example.com",
            "555-555-5555",
            DateTime.UtcNow
        ),
        new(
            Guid.Parse("a1b2c3d4-e5f6-7890-a1b2-c3d4e5f67890"),
            "Bob",
            "Williams",
            "bob.williams@example.com",
            "111-111-1111",
            DateTime.UtcNow
        ),
        new(
            Guid.Parse("b2c3d4e5-f678-9012-a1b2-c3d4e5f67890"),
            "Charlie",
            "Brown",
            "charlie.brown@example.com",
            "222-222-2222",
            DateTime.UtcNow
        ),
    ];

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

        var newCustomer = new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAt
        );

        _customers.Add(newCustomer);
        return await Task.FromResult(newCustomer);
    }

    public async Task<List<CustomerResponse>> GetAllCustomersAsync()
    {
        return await Task.FromResult(_customers);
    }

    // public async Task<CustomerResponse?> GetCustomerByIdAsync(Guid id)
    // {
    //     var customer = _customers.FirstOrDefault(c => c.Id == id);
    //     return await Task.FromResult(customer);
    // }

    // public async Task<CustomerResponse?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    // {
    //     var customer = _customers.FirstOrDefault(c => c.Id == id);
    //     if (customer is null)
    //         return null;

    //     var updatedCustomer = customer with
    //     {
    //         FirstName = request.FirstName ?? customer.FirstName,
    //         LastName = request.LastName ?? customer.LastName,
    //         Email = request.Email ?? customer.Email,
    //         PhoneNumber = request.PhoneNumber ?? customer.PhoneNumber,
    //     };

    //     _customers.Remove(customer);
    //     _customers.Add(updatedCustomer);

    //     return await Task.FromResult(updatedCustomer);
    // }

    // public async Task<bool> DeleteCustomerAsync(Guid id)
    // {
    //     var customer = _customers.FirstOrDefault(c => c.Id == id);
    //     if (customer is null)
    //         return false;

    //     _customers.Remove(customer);
    //     return await Task.FromResult(true);
    // }
}
