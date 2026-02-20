using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ReasonCodeManagementIntegrationTests
{
    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateUseAttemptDelete_ShouldBlockDeleteWhenUsageExists()
    {
        var dbName = $"reason-code-integration-{Guid.NewGuid():N}";
        var currentUser = new TestCurrentUserService();

        await using var db = CreateDbContext(dbName, currentUser);
        var service = new ReasonCodeService(db, NullLoggerFactory.Instance.CreateLogger<ReasonCodeService>());

        var created = await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE-FORKLIFT",
            "Forklift Damage",
            "Damage during handling",
            null,
            "ADJUSTMENT",
            true));

        created.IsSuccess.Should().BeTrue();

        var usage = await service.IncrementUsageAsync("DAMAGE-FORKLIFT", ReasonCategory.ADJUSTMENT);
        usage.IsSuccess.Should().BeTrue();

        var delete = await service.DeleteAsync(created.Value.Id);

        delete.IsSuccess.Should().BeFalse();
        delete.ErrorDetail.Should().Be("Cannot delete reason code with usage history. Mark inactive instead.");
    }

    private static WarehouseDbContext CreateDbContext(string dbName, ICurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new WarehouseDbContext(options, currentUser);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "integration-user";
    }
}
