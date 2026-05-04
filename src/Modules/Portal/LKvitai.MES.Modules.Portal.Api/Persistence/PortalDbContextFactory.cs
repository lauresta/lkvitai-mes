using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public sealed class PortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PortalDb") ??
            Environment.GetEnvironmentVariable("ConnectionStrings:PortalDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__PortalDb before running Portal EF commands.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .Options;

        return new PortalDbContext(options);
    }
}
