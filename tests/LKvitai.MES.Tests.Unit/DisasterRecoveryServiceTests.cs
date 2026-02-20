using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public sealed class DisasterRecoveryServiceTests
{
    [Fact]
    public async Task TriggerDrillAsync_CompletesAndRecordsSteps()
    {
        await using var db = CreateDbContext();
        var (_, _, sut) = CreateService(db);

        var result = await sut.TriggerDrillAsync(DisasterScenario.DatabaseCorruption);

        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal("DATABASE_CORRUPTION", result.Scenario);
        Assert.Contains("Step 1 complete", result.Notes);
        Assert.Contains("Step 2 complete", result.Notes);
        Assert.Contains("Step 3 complete", result.Notes);
    }

    [Fact]
    public async Task TriggerDrillAsync_DataCenterOutage_AddsDnsAutomationIssue()
    {
        await using var db = CreateDbContext();
        var (_, _, sut) = CreateService(db);

        var result = await sut.TriggerDrillAsync(DisasterScenario.DataCenterOutage);

        Assert.Contains("DNS switch automation failed", result.IssuesIdentified);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNewestFirst()
    {
        await using var db = CreateDbContext();
        var (_, _, sut) = CreateService(db);

        await sut.TriggerDrillAsync(DisasterScenario.Ransomware);
        await Task.Delay(5);
        await sut.TriggerDrillAsync(DisasterScenario.DatabaseCorruption);

        var history = await sut.GetHistoryAsync();

        Assert.True(history.Count >= 2);
        Assert.True(history[0].DrillStartedAt >= history[1].DrillStartedAt);
    }

    [Fact]
    public async Task RunQuarterlyDrillAsync_UsesDataCenterOutageScenario()
    {
        await using var db = CreateDbContext();
        var (_, _, sut) = CreateService(db);

        var result = await sut.RunQuarterlyDrillAsync();

        Assert.Equal("DATA_CENTER_OUTAGE", result.Scenario);
    }

    [Fact]
    public async Task TriggerDrillAsync_WritesAuditEvents()
    {
        await using var db = CreateDbContext();
        var (auditLog, _, sut) = CreateService(db);

        await sut.TriggerDrillAsync(DisasterScenario.DatabaseCorruption);

        auditLog.Verify(
            x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private static (Mock<ISecurityAuditLogService> auditLog, Mock<ICurrentUserService> currentUser, DisasterRecoveryService sut) CreateService(WarehouseDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        audit.Setup(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<DisasterRecoveryService>>(MockBehavior.Loose);
        var sut = new DisasterRecoveryService(db, currentUser.Object, audit.Object, logger.Object);
        return (audit, currentUser, sut);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"dr-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
