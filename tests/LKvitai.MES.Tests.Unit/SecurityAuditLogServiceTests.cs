using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class SecurityAuditLogServiceTests
{
    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task WriteAsync_ShouldPersistRecord()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        await sut.WriteAsync(new SecurityAuditLogWriteRequest(
            "user-1",
            "CREATE_ITEM",
            "ITEM",
            "1",
            "127.0.0.1",
            "UnitTest",
            DateTimeOffset.UtcNow,
            "{}"));

        var count = await db.SecurityAuditLogs.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task WriteAsync_WhenFieldsMissing_ShouldApplyFallbacks()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        await sut.WriteAsync(new SecurityAuditLogWriteRequest(
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            default,
            string.Empty));

        var row = await db.SecurityAuditLogs.SingleAsync();
        row.Action.Should().Be("UNKNOWN");
        row.Resource.Should().Be("SYSTEM");
        row.IpAddress.Should().Be("unknown");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenFilteredByUserId_ShouldReturnOnlyMatching()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery("user-1", null, null, null, null, null));

        rows.Should().OnlyContain(x => x.UserId == "user-1");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenFilteredByAction_ShouldReturnOnlyMatching()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, "CREATE_ITEM", null, null, null, null));

        rows.Should().OnlyContain(x => x.Action == "CREATE_ITEM");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenFilteredByResource_ShouldReturnOnlyMatching()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, null, "ORDER", null, null, null));

        rows.Should().OnlyContain(x => x.Resource == "ORDER");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenDateRangeSpecified_ShouldRespectBoundaries()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        var start = DateTimeOffset.UtcNow.AddDays(-5);
        var mid = DateTimeOffset.UtcNow.AddDays(-2);
        var end = DateTimeOffset.UtcNow;

        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "A", "ITEM", null, "ip", "ua", start, "{}"));
        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "B", "ITEM", null, "ip", "ua", mid, "{}"));
        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "C", "ITEM", null, "ip", "ua", end, "{}"));

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, null, null, DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1), null));

        rows.Should().ContainSingle();
        rows[0].Action.Should().Be("B");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenLimitProvided_ShouldApplyLimit()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut, count: 20);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, null, null, null, null, 5));

        rows.Should().HaveCount(5);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_ShouldSortByTimestampDescending()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "OLDER", "ITEM", null, "ip", "ua", DateTimeOffset.UtcNow.AddMinutes(-10), "{}"));
        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "NEWER", "ITEM", null, "ip", "ua", DateTimeOffset.UtcNow, "{}"));

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, null, null, null, null, null));

        rows[0].Action.Should().Be("NEWER");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenLimitInvalid_ShouldUseDefault()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut, count: 3);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery(null, null, null, null, null, -1));

        rows.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenCombinedFilters_ShouldReturnIntersection()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "CREATE_ITEM", "ITEM", "1", "ip", "ua", DateTimeOffset.UtcNow, "{}"));
        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u2", "CREATE_ITEM", "ITEM", "2", "ip", "ua", DateTimeOffset.UtcNow, "{}"));
        await sut.WriteAsync(new SecurityAuditLogWriteRequest("u1", "UPDATE_ORDER", "ORDER", "3", "ip", "ua", DateTimeOffset.UtcNow, "{}"));

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery("u1", "CREATE_ITEM", "ITEM", null, null, null));

        rows.Should().ContainSingle();
        rows[0].UserId.Should().Be("u1");
        rows[0].Action.Should().Be("CREATE_ITEM");
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task QueryAsync_WhenNoMatches_ShouldReturnEmpty()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await SeedAsync(sut);

        var rows = await sut.QueryAsync(new SecurityAuditLogQuery("unknown", null, null, null, null, null));

        rows.Should().BeEmpty();
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"audit-log-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static SecurityAuditLogService CreateService(WarehouseDbContext db)
        => new(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityAuditLogService>.Instance);

    private static async Task SeedAsync(SecurityAuditLogService sut, int count = 10)
    {
        for (var i = 0; i < count; i++)
        {
            var userId = i % 2 == 0 ? "user-1" : "user-2";
            var action = i % 2 == 0 ? "CREATE_ITEM" : "UPDATE_ORDER";
            var resource = i % 2 == 0 ? "ITEM" : "ORDER";

            await sut.WriteAsync(new SecurityAuditLogWriteRequest(
                userId,
                action,
                resource,
                i.ToString(),
                "127.0.0.1",
                "UnitTest",
                DateTimeOffset.UtcNow.AddMinutes(-i),
                "{}"));
        }
    }
}
