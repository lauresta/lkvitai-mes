using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;

public sealed class ShopfloorDbContextFactory : IDesignTimeDbContextFactory<ShopfloorDbContext>
{
    public ShopfloorDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ShopfloorDb") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:ShopfloorDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__ShopfloorDb before running Shopfloor EF commands.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var options = new DbContextOptionsBuilder<ShopfloorDbContext>()
            .UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ShopfloorDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .Options;

        return new ShopfloorDbContext(options);
    }
}
