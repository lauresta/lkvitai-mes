using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace LKvitai.MES.Infrastructure.Persistence;

public sealed class WarehouseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WarehouseDbContext>
{
    public WarehouseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WarehouseDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=warehouse;Username=postgres;Password=postgres";
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!builder.ContainsKey("Port") || builder.Port == 0)
        {
            builder.Port = 5432;
        }

        var optionsBuilder = new DbContextOptionsBuilder<WarehouseDbContext>();
        optionsBuilder.UseNpgsql(
            builder.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));

        return new WarehouseDbContext(optionsBuilder.Options);
    }
}
