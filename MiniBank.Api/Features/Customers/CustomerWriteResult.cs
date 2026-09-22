namespace MiniBank.Api.Features.Customers;

public enum CustomerWriteStatus
{
    Success,
    NotFound,
    DuplicateEmail,
    InvalidRequest,
}

public sealed record CustomerWriteResult(
    CustomerWriteStatus Status,
    CustomerResponse? Customer = null,
    IDictionary<string, string[]>? Errors = null
);

public enum CustomerDeleteResult
{
    Deleted,
    NotFound,
    HasAccounts,
}
