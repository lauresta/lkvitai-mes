using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LKvitai.MES.Infrastructure.Persistence;

public sealed class WarehouseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WarehouseDbContext>
{
    public WarehouseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WarehouseDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=warehouse;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));

        return new WarehouseDbContext(optionsBuilder.Options);
    }
}
