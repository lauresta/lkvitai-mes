using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class WarehouseSettingsIntegrationTests
{
    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateSettings_ShouldRefreshCacheAcrossServiceScopes()
    {
        var databaseName = $"warehouse-settings-int-{Guid.NewGuid():N}";
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var currentUser = new TestCurrentUserService();

        await using (var db = CreateDbContext(databaseName, currentUser))
        {
            var service = CreateService(db, cache, currentUser);
            var initial = await service.GetAsync();
            initial.CapacityThresholdPercent.Should().Be(80);
        }

        await using (var externalDb = CreateDbContext(databaseName, currentUser))
        {
            var entity = await externalDb.WarehouseSettings.SingleAsync();
            entity.CapacityThresholdPercent = 95;
            await externalDb.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName, currentUser))
        {
            var service = CreateService(db, cache, currentUser);
            var cached = await service.GetAsync();
            cached.CapacityThresholdPercent.Should().Be(80);
        }

        await using (var db = CreateDbContext(databaseName, currentUser))
        {
            var service = CreateService(db, cache, currentUser);
            var update = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
                87,
                "FIFO",
                9,
                44,
                false));

            update.IsSuccess.Should().BeTrue();
            update.Value.CapacityThresholdPercent.Should().Be(87);
        }

        await using (var db = CreateDbContext(databaseName, currentUser))
        {
            var service = CreateService(db, cache, currentUser);
            var refreshed = await service.GetAsync();
            refreshed.CapacityThresholdPercent.Should().Be(87);
            refreshed.DefaultPickStrategy.Should().Be("FIFO");
            refreshed.AutoAllocateOrders.Should().BeFalse();
        }
    }

    private static WarehouseDbContext CreateDbContext(string databaseName, ICurrentUserService currentUserService)
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new WarehouseDbContext(options, currentUserService);
    }

    private static WarehouseSettingsService CreateService(
        WarehouseDbContext db,
        IMemoryCache cache,
        ICurrentUserService currentUserService)
    {
        return new WarehouseSettingsService(
            db,
            cache,
            currentUserService,
            NullLoggerFactory.Instance.CreateLogger<WarehouseSettingsService>());
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "integration-user";
    }
}
