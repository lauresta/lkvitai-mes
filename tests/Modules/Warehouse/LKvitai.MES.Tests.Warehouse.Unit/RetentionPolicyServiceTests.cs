using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class RetentionPolicyServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPolicy()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(new CreateRetentionPolicyRequest(
            RetentionDataType.AuditLogs,
            2555,
            365,
            null,
            true));

        Assert.Equal("AUDITLOGS", created.DataType.Replace("_", string.Empty));
        Assert.Equal(2555, created.RetentionPeriodDays);
        Assert.Single(db.RetentionPolicies);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivesAndDeletesExpiredAuditLogs()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        db.RetentionPolicies.Add(new RetentionPolicy
        {
            DataType = RetentionDataType.AuditLogs,
            RetentionPeriodDays = 30,
            ArchiveAfterDays = 7,
            DeleteAfterDays = 30,
            Active = true,
            CreatedBy = "tester"
        });

        db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            Action = "LOGIN",
            Resource = "AUTH",
            IpAddress = "127.0.0.1",
            UserAgent = "unit-test",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
            Details = "{}"
        });

        db.AuditLogArchives.Add(new AuditLogArchive
        {
            Id = 777,
            Action = "OLD",
            Resource = "AUTH",
            IpAddress = "127.0.0.1",
            UserAgent = "unit-test",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-60),
            Details = "{}"
        });

        await db.SaveChangesAsync();

        var execution = await service.ExecuteAsync();

        Assert.Equal("COMPLETED", execution.Status);
        Assert.Equal(1, execution.RecordsArchived);
        Assert.Equal(1, execution.RecordsDeleted);
        Assert.Empty(db.SecurityAuditLogs);
        Assert.Single(db.AuditLogArchives.Where(x => x.Id != 777));
    }

    [Fact]
    public async Task ExecuteAsync_SkipsLegalHoldRows()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        db.RetentionPolicies.Add(new RetentionPolicy
        {
            DataType = RetentionDataType.AuditLogs,
            RetentionPeriodDays = 30,
            ArchiveAfterDays = 7,
            Active = true,
            CreatedBy = "tester"
        });

        db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            Action = "LOGIN",
            Resource = "AUTH",
            IpAddress = "127.0.0.1",
            UserAgent = "unit-test",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
            LegalHold = true,
            Details = "{}"
        });

        await db.SaveChangesAsync();

        var execution = await service.ExecuteAsync();

        Assert.Equal(0, execution.RecordsArchived);
        Assert.Single(db.SecurityAuditLogs);
        Assert.Empty(db.AuditLogArchives);
    }

    private static RetentionPolicyService CreateService(WarehouseDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("unit-test-admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Loose);

        return new RetentionPolicyService(
            db,
            currentUser.Object,
            audit.Object,
            new Mock<ILogger<RetentionPolicyService>>(MockBehavior.Loose).Object);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"retention-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
