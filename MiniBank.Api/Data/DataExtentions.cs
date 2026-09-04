using Microsoft.EntityFrameworkCore;

namespace MiniBank.Api.Data;

public static class DataExtentions
{
    public static void Migrate(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.Migrate();
    }

    public static void AddAppDb(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DbConnection");

        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
    }
}
