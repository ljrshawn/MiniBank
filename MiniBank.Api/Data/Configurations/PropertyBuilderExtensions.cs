using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiniBank.Api.Data.Configurations;

internal static class PropertyBuilderExtensions
{
    internal static PropertyBuilder<DateTime> HasUtcConversion(
        this PropertyBuilder<DateTime> property
    ) => property.HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
