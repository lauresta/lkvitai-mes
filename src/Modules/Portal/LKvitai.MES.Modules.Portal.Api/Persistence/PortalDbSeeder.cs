using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public static class PortalDbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Tiles.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        db.Tiles.AddRange(PortalSeedData.Tiles(now));
        await db.SaveChangesAsync(cancellationToken);
    }
}
