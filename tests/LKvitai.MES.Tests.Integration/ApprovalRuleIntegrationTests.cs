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

public class ApprovalRuleIntegrationTests
{
    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateRuleThenEvaluate_ShouldReturnExpectedApprover()
    {
        var dbName = $"approval-rules-integration-{Guid.NewGuid():N}";
        var currentUser = new TestCurrentUserService();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await using var db = CreateDbContext(dbName, currentUser);
        var service = new ApprovalRuleService(db, cache, NullLoggerFactory.Instance.CreateLogger<ApprovalRuleService>());

        var created = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            10000m,
            "Manager",
            true,
            1));

        created.IsSuccess.Should().BeTrue();

        var positive = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("COST_ADJUSTMENT", 15000m));
        positive.IsSuccess.Should().BeTrue();
        positive.Value.RequiresApproval.Should().BeTrue();
        positive.Value.ApproverRole.Should().Be("Manager");

        var negative = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("COST_ADJUSTMENT", 5000m));
        negative.IsSuccess.Should().BeTrue();
        negative.Value.RequiresApproval.Should().BeFalse();
        negative.Value.ApproverRole.Should().BeNull();
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
