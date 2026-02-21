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

public class ApprovalRuleServiceTests
{
    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateAsync_WhenValid_ShouldSucceed()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            10000m,
            "Manager",
            true,
            1));

        result.IsSuccess.Should().BeTrue();
        result.Value.RuleType.Should().Be("COST_ADJUSTMENT");
        result.Value.ApproverRole.Should().Be("Manager");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateAsync_WhenThresholdNegative_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            -1m,
            "Manager",
            true,
            1));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateAsync_WhenApproverRoleInvalid_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            1000m,
            "UnknownRole",
            true,
            1));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("ApproverRole");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task CreateAsync_WhenRuleTypeInvalid_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateApprovalRuleRequest(
            "INVALID",
            "AMOUNT",
            1000m,
            "Manager",
            true,
            1));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("RuleType");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task UpdateAsync_WhenRuleMissing_ShouldFailNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateAsync(42, new UpdateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            500m,
            "Manager",
            true,
            1));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task UpdateAsync_WhenValid_ShouldPersist()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            10000m,
            "Manager",
            true,
            1));

        var updated = await service.UpdateAsync(created.Value.Id, new UpdateApprovalRuleRequest(
            "WRITEDOWN",
            "AMOUNT",
            2000m,
            "CFO",
            false,
            2));

        updated.IsSuccess.Should().BeTrue();
        updated.Value.RuleType.Should().Be("WRITEDOWN");
        updated.Value.Active.Should().BeFalse();
        updated.Value.Priority.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task DeleteAsync_WhenExisting_ShouldRemoveRule()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateAsync(new CreateApprovalRuleRequest(
            "TRANSFER",
            "AMOUNT",
            100m,
            "Manager",
            true,
            1));

        var deleted = await service.DeleteAsync(created.Value.Id);

        deleted.IsSuccess.Should().BeTrue();

        await using var verifyDb = fixture.CreateDbContext();
        var exists = await verifyDb.ApprovalRules.AnyAsync(x => x.Id == created.Value.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenRuleTypeInvalid_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("BAD", 100m));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("RuleType");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenValueNegative_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("TRANSFER", -1m));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Value must be >= 0");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenBelowThreshold_ShouldNotRequireApproval()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            10000m,
            "Manager",
            true,
            1));

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("COST_ADJUSTMENT", 5000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresApproval.Should().BeFalse();
        result.Value.ApproverRole.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenAboveThreshold_ShouldReturnApproverRole()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateApprovalRuleRequest(
            "COST_ADJUSTMENT",
            "AMOUNT",
            10000m,
            "Manager",
            true,
            1));

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("COST_ADJUSTMENT", 15000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresApproval.Should().BeTrue();
        result.Value.ApproverRole.Should().Be("Manager");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenMultipleRules_ShouldRespectPriority()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateApprovalRuleRequest("COST_ADJUSTMENT", "AMOUNT", 1000m, "Manager", true, 2));
        _ = await service.CreateAsync(new CreateApprovalRuleRequest("COST_ADJUSTMENT", "AMOUNT", 500m, "CFO", true, 1));

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("COST_ADJUSTMENT", 2000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.ApproverRole.Should().Be("CFO");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenSamePriority_ShouldPickHigherThreshold()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateApprovalRuleRequest("TRANSFER", "AMOUNT", 100m, "Manager", true, 1));
        _ = await service.CreateAsync(new CreateApprovalRuleRequest("TRANSFER", "AMOUNT", 500m, "CFO", true, 1));

        var result = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("TRANSFER", 1000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.ApproverRole.Should().Be("CFO");
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task GetAsync_ShouldReturnSortedByPriority()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateApprovalRuleRequest("TRANSFER", "AMOUNT", 100m, "Manager", true, 5));
        _ = await service.CreateAsync(new CreateApprovalRuleRequest("TRANSFER", "AMOUNT", 200m, "CFO", true, 1));

        var rules = await service.GetAsync();

        rules.Should().HaveCount(2);
        rules[0].Priority.Should().Be(1);
        rules[1].Priority.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenRuleCreatedAfterCacheWarmup_ShouldUseFreshCache()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var initial = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("WRITEDOWN", 2000m));
        initial.Value.RequiresApproval.Should().BeFalse();

        _ = await service.CreateAsync(new CreateApprovalRuleRequest("WRITEDOWN", "AMOUNT", 1000m, "Manager", true, 1));

        var afterCreate = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("WRITEDOWN", 2000m));
        afterCreate.Value.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "ApprovalRules")]
    public async Task EvaluateAsync_WhenRuleUpdatedOrDeleted_ShouldRefreshCache()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateAsync(new CreateApprovalRuleRequest("TRANSFER", "AMOUNT", 100m, "Manager", true, 1));

        var beforeUpdate = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("TRANSFER", 200m));
        beforeUpdate.Value.RequiresApproval.Should().BeTrue();

        _ = await service.UpdateAsync(created.Value.Id, new UpdateApprovalRuleRequest("TRANSFER", "AMOUNT", 500m, "Manager", true, 1));

        var afterUpdate = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("TRANSFER", 200m));
        afterUpdate.Value.RequiresApproval.Should().BeFalse();

        _ = await service.DeleteAsync(created.Value.Id);

        var afterDelete = await service.EvaluateAsync(new EvaluateApprovalRuleRequest("TRANSFER", 1000m));
        afterDelete.Value.RequiresApproval.Should().BeFalse();
    }

    private sealed class TestFixture
    {
        private readonly string _dbName = $"approval-rules-tests-{Guid.NewGuid():N}";
        private readonly ICurrentUserService _currentUserService = new TestCurrentUserService();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            return new WarehouseDbContext(options, _currentUserService);
        }

        public ApprovalRuleService CreateService(WarehouseDbContext db)
        {
            return new ApprovalRuleService(
                db,
                _cache,
                NullLoggerFactory.Instance.CreateLogger<ApprovalRuleService>());
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "test-user";
    }
}
