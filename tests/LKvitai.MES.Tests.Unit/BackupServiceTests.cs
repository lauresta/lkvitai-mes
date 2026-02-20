using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task TriggerBackupAsync_CreatesCompletedExecution()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        var result = await sut.TriggerBackupAsync("MANUAL");

        Assert.Equal("COMPLETED", result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.BackupSizeBytes >= 0);
        Assert.NotNull(result.BlobPath);
    }

    [Fact]
    public async Task RestoreAsync_WhenMissingBackup_ReturnsFailure()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        var result = await sut.RestoreAsync(Guid.NewGuid(), "test");

        Assert.False(result.Success);
    }

    private static BackupService CreateService(WarehouseDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Loose);
        var logger = new Mock<ILogger<BackupService>>(MockBehavior.Loose);

        return new BackupService(db, currentUser.Object, audit.Object, logger.Object);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"backup-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
