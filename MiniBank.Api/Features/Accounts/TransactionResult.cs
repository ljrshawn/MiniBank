namespace MiniBank.Api.Features.Accounts;

public enum TransactionStatus
{
    Success,
    NotFound,
    InvalidRequest,
    InsufficientFunds,
    BalanceLimitExceeded,
    Conflict,
}

public sealed record TransactionResult(
    TransactionStatus Status,
    TransactionResponse? Transaction = null,
    string? ErrorMessage = null
);
