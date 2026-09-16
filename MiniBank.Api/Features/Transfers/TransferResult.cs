namespace MiniBank.Api.Features.Transfers;

public enum TransferResultStatus
{
    Success,
    NotFound,
    InvalidRequest,
    InsufficientFunds,
    BalanceLimitExceeded,
    Conflict,
}

public sealed record TransferResult(
    TransferResultStatus Status,
    TransferResponse? Transfer = null,
    string? ErrorMessage = null
);
