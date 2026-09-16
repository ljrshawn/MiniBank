namespace MiniBank.Api.Entities;

internal static class MoneyLimits
{
    internal const decimal MaximumAmount = 999_999_999_999m;

    // Matches the numeric(18, 2) columns used for balances and transaction history.
    internal const decimal MaximumBalance = 9_999_999_999_999_999.99m;
}
