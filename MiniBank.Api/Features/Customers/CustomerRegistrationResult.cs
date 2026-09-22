using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Customers;

public sealed record CustomerRegistrationResult(
    CustomerWriteStatus Status,
    Customer? Customer = null,
    IDictionary<string, string[]>? Errors = null
);
