namespace MiniBank.Api.Features.Customers;

public enum CustomerWriteStatus
{
    Success,
    NotFound,
    DuplicateEmail,
}

public sealed record CustomerWriteResult(
    CustomerWriteStatus Status,
    CustomerResponse? Customer = null
);

public enum CustomerDeleteResult
{
    Deleted,
    NotFound,
    HasAccounts,
}
