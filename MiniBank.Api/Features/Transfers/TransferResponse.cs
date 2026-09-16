using System.Linq.Expressions;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Transfers;

public sealed record TransferResponse(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime TransactionDate,
    string Description,
    DateTime CreatedAt
)
{
    internal static readonly Expression<Func<Transfer, TransferResponse>> Projection =
        transfer => new TransferResponse(
            transfer.Id,
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.Amount,
            transfer.TransactionDate,
            transfer.Description,
            transfer.CreatedAt
        );

    internal static TransferResponse FromEntity(Transfer transfer) => Map(transfer);

    private static readonly Func<Transfer, TransferResponse> Map = Projection.Compile();
}
