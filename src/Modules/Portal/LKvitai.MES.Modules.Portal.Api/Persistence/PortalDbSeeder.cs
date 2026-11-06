using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public static class PortalDbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (await db.Tiles.AnyAsync(cancellationToken))
        {
            await PromoteShopfloorTileAsync(db, now, cancellationToken);
            await PromoteFinanceTileAsync(db, now, cancellationToken);
            return;
        }

        db.Tiles.AddRange(PortalSeedData.Tiles(now));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task PromoteFinanceTileAsync(
        PortalDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var finance = await db.Tiles
            .FirstOrDefaultAsync(t => t.Key == "finance", cancellationToken);

        if (finance is null)
        {
            db.Tiles.Add(PortalSeedData.Tiles(now).First(t => t.Key == "finance"));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Preserve operator edits: promote only the original planned seed row.
        if (!string.IsNullOrWhiteSpace(finance.Url) ||
            !string.Equals(finance.Status, "Planned", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        finance.Status = "Pilot";
        finance.Url = "/finance/";
        finance.Quarter = null;
        finance.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task PromoteShopfloorTileAsync(
        PortalDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var shopfloor = await db.Tiles
            .FirstOrDefaultAsync(t => t.Key == "shopfloor", cancellationToken);

        if (shopfloor is null)
        {
            var seed = PortalSeedData.Tiles(now).First(t => t.Key == "shopfloor");
            db.Tiles.Add(seed);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(shopfloor.Url) ||
            !string.Equals(shopfloor.Status, "Planned", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        shopfloor.Status = "Pilot";
        shopfloor.Url = "/shopfloor/";
        shopfloor.Quarter = null;
        shopfloor.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }
}
