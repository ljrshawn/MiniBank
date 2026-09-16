using System.ComponentModel.DataAnnotations;
using MiniBank.Api.Entities;

namespace MiniBank.Api.Features.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MoneyAmountAttribute : ValidationAttribute
{
    public MoneyAmountAttribute()
        : base(
            "Amount must be greater than zero and no more than 999999999999, with at most two decimal places."
        )
    { }

    public override bool IsValid(object? value) =>
        value is decimal amount
        && amount > 0m
        && amount <= MoneyLimits.MaximumAmount
        && decimal.Round(amount, 2) == amount;
}
