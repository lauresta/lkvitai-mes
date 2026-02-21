using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class WarehouseSettingsServiceTests
{
    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task GetAsync_WhenSettingsMissing_ShouldReturnDefaults()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.GetAsync();

        result.Id.Should().Be(1);
        result.CapacityThresholdPercent.Should().Be(80);
        result.DefaultPickStrategy.Should().Be("FEFO");
        result.LowStockThreshold.Should().Be(10);
        result.ReorderPoint.Should().Be(50);
        result.AutoAllocateOrders.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task GetAsync_WhenSettingsMissing_ShouldCreateSingletonRow()
    {
        var fixture = new TestFixture();
        await using (var db = fixture.CreateDbContext())
        {
            var service = fixture.CreateService(db);
            _ = await service.GetAsync();
        }

        await using var verifyDb = fixture.CreateDbContext();
        var rows = await verifyDb.WarehouseSettings.AsNoTracking().ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Id.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task GetAsync_WhenCached_ShouldNotReadExternalChanges()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var first = await service.GetAsync();
        first.CapacityThresholdPercent.Should().Be(80);

        await using (var externalDb = fixture.CreateDbContext())
        {
            var entity = await externalDb.WarehouseSettings.SingleAsync();
            entity.CapacityThresholdPercent = 99;
            await externalDb.SaveChangesAsync();
        }

        var second = await service.GetAsync();
        second.CapacityThresholdPercent.Should().Be(80);
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenCapacityBelowZero_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            -1,
            "FEFO",
            10,
            50,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Capacity threshold must be 0-100%.");
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenCapacityAboveHundred_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            101,
            "FEFO",
            10,
            50,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Capacity threshold must be 0-100%.");
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenLowStockNegative_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            80,
            "FEFO",
            -2,
            50,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Low stock threshold must be >= 0.");
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenReorderPointNegative_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            80,
            "FEFO",
            10,
            -1,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Reorder point must be >= 0.");
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenPickStrategyInvalid_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            80,
            "LIFO",
            10,
            50,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Default pick strategy must be FEFO or FIFO.");
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenValid_ShouldUpdateValuesAndRefreshCache()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.GetAsync();

        await using (var externalDb = fixture.CreateDbContext())
        {
            var entity = await externalDb.WarehouseSettings.SingleAsync();
            entity.CapacityThresholdPercent = 77;
            await externalDb.SaveChangesAsync();
        }

        var update = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            85,
            "FIFO",
            12,
            55,
            false));

        update.IsSuccess.Should().BeTrue();
        update.Value.CapacityThresholdPercent.Should().Be(85);
        update.Value.DefaultPickStrategy.Should().Be("FIFO");
        update.Value.AutoAllocateOrders.Should().BeFalse();

        var readBack = await service.GetAsync();
        readBack.CapacityThresholdPercent.Should().Be(85);
        readBack.DefaultPickStrategy.Should().Be("FIFO");
        readBack.LowStockThreshold.Should().Be(12);
        readBack.ReorderPoint.Should().Be(55);
        readBack.AutoAllocateOrders.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenValid_ShouldSetUpdatedBy()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.GetAsync();
        fixture.CurrentUser.UserId = "admin-user";

        var result = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(
            82,
            "FEFO",
            14,
            60,
            true));

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedBy.Should().Be("admin-user");
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "WarehouseSettings")]
    public async Task UpdateAsync_WhenCalledMultipleTimes_ShouldKeepSingleton()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(81, "FEFO", 10, 50, true));
        _ = await service.UpdateAsync(new UpdateWarehouseSettingsRequest(83, "FIFO", 9, 40, false));

        await using var verifyDb = fixture.CreateDbContext();
        var entities = await verifyDb.WarehouseSettings.AsNoTracking().ToListAsync();

        entities.Should().HaveCount(1);
        entities[0].Id.Should().Be(1);
        entities[0].CapacityThresholdPercent.Should().Be(83);
    }

    private sealed class TestFixture
    {
        private readonly string _databaseName = $"warehouse-settings-tests-{Guid.NewGuid():N}";

        public TestCurrentUserService CurrentUser { get; } = new();

        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;

            return new WarehouseDbContext(options, CurrentUser);
        }

        public WarehouseSettingsService CreateService(WarehouseDbContext dbContext)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new WarehouseSettingsService(
                dbContext,
                cache,
                CurrentUser,
                NullLoggerFactory.Instance.CreateLogger<WarehouseSettingsService>());
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "test-user";

        public string GetCurrentUserId() => UserId;
    }
}
