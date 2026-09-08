namespace MiniBank.Api.Features.Accounts;

internal sealed class AccountNumberAllocationException()
    : Exception("Unable to allocate a unique account number after five attempts.");
