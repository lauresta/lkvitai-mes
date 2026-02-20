using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ReasonCodeServiceTests
{
    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenValidRoot_ShouldSucceed()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateReasonCodeRequest(
            "damage-forklift",
            "Forklift Damage",
            "Damage by forklift",
            null,
            "ADJUSTMENT",
            true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("DAMAGE-FORKLIFT");
        result.Value.ParentId.Should().BeNull();
        result.Value.UsageCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenCodeDuplicate_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("DAMAGE", "Damage", null, null, "ADJUSTMENT", true));
        var result = await service.CreateAsync(new CreateReasonCodeRequest("damage", "Duplicate", null, null, "ADJUSTMENT", true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("already exists");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenNameTooShort_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateReasonCodeRequest("DAMAGE", "AB", null, null, "ADJUSTMENT", true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Be("Name must be at least 3 characters.");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenCategoryInvalid_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateReasonCodeRequest("DAMAGE", "Damage", null, null, "INVALID", true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Category must be");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenParentMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE-FORKLIFT",
            "Forklift Damage",
            null,
            999,
            "ADJUSTMENT",
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Parent reason code '999' does not exist");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task CreateAsync_WhenParentHasParent_ShouldFailMaxTwoLevels()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var parent = (await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE",
            "Damage",
            null,
            null,
            "ADJUSTMENT",
            true))).Value;

        var child = (await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE-FORKLIFT",
            "Forklift Damage",
            null,
            parent.Id,
            "ADJUSTMENT",
            true))).Value;

        var result = await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE-FORKLIFT-HEAVY",
            "Heavy Forklift Damage",
            null,
            child.Id,
            "ADJUSTMENT",
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Be("Hierarchy supports max 2 levels (parent -> child).");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task UpdateAsync_WhenSelfParent_ShouldFailCircular()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = (await service.CreateAsync(new CreateReasonCodeRequest(
            "RETURN",
            "Return",
            null,
            null,
            "RETURN",
            true))).Value;

        var result = await service.UpdateAsync(created.Id, new UpdateReasonCodeRequest(
            created.Code,
            created.Name,
            created.Description,
            created.Id,
            created.Category,
            created.Active));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Be("Circular reference is not allowed.");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task UpdateAsync_WhenAssignParentToNodeWithChildren_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var rootA = (await service.CreateAsync(new CreateReasonCodeRequest("A", "Root A", null, null, "ADJUSTMENT", true))).Value;
        var rootB = (await service.CreateAsync(new CreateReasonCodeRequest("B", "Root B", null, null, "ADJUSTMENT", true))).Value;
        _ = await service.CreateAsync(new CreateReasonCodeRequest("A-1", "Child A", null, rootA.Id, "ADJUSTMENT", true));

        var result = await service.UpdateAsync(rootA.Id, new UpdateReasonCodeRequest(
            rootA.Code,
            rootA.Name,
            null,
            rootB.Id,
            rootA.Category,
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("already has children");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task UpdateAsync_WhenDuplicateCode_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var one = (await service.CreateAsync(new CreateReasonCodeRequest("ONE", "One", null, null, "ADJUSTMENT", true))).Value;
        var two = (await service.CreateAsync(new CreateReasonCodeRequest("TWO", "Two", null, null, "ADJUSTMENT", true))).Value;

        var result = await service.UpdateAsync(two.Id, new UpdateReasonCodeRequest(
            one.Code,
            two.Name,
            null,
            null,
            "ADJUSTMENT",
            true));

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("already exists");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task DeleteAsync_WhenUsageExists_ShouldFailSoftDeleteRule()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = (await service.CreateAsync(new CreateReasonCodeRequest(
            "DAMAGE",
            "Damage",
            null,
            null,
            "ADJUSTMENT",
            true))).Value;

        _ = await service.IncrementUsageAsync("DAMAGE", ReasonCategory.ADJUSTMENT);

        var result = await service.DeleteAsync(created.Id);

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Be("Cannot delete reason code with usage history. Mark inactive instead.");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task DeleteAsync_WhenUnused_ShouldRemove()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = (await service.CreateAsync(new CreateReasonCodeRequest(
            "RETURN",
            "Return",
            null,
            null,
            "RETURN",
            true))).Value;

        var result = await service.DeleteAsync(created.Id);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = fixture.CreateDbContext();
        var exists = await verifyDb.AdjustmentReasonCodes.AnyAsync(x => x.Id == created.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task GetAsync_WhenFilteredByActive_ShouldReturnOnlyActiveRows()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("ACTIVE", "Active", null, null, "ADJUSTMENT", true));
        _ = await service.CreateAsync(new CreateReasonCodeRequest("INACTIVE", "Inactive", null, null, "ADJUSTMENT", false));

        var result = await service.GetAsync(null, true);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Code.Should().Be("ACTIVE");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task GetAsync_WhenFilteredByCategory_ShouldReturnOnlyCategoryRows()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("ADJ", "Adjustment", null, null, "ADJUSTMENT", true));
        _ = await service.CreateAsync(new CreateReasonCodeRequest("RET", "Return", null, null, "RETURN", true));

        var result = await service.GetAsync("RETURN", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Category.Should().Be("RETURN");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task GetAsync_WhenCategoryInvalid_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.GetAsync("BAD", null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Category must be");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task IncrementUsageAsync_WhenValid_ShouldIncreaseUsageCount()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("DAMAGE", "Damage", null, null, "ADJUSTMENT", true));

        var result = await service.IncrementUsageAsync("damage", ReasonCategory.ADJUSTMENT);

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = fixture.CreateDbContext();
        var row = await verifyDb.AdjustmentReasonCodes.SingleAsync(x => x.Code == "DAMAGE");
        row.UsageCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task IncrementUsageAsync_WhenCategoryMismatch_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("RETURN", "Return", null, null, "RETURN", true));

        var result = await service.IncrementUsageAsync("RETURN", ReasonCategory.ADJUSTMENT);

        result.IsSuccess.Should().BeFalse();
        result.ErrorDetail.Should().Contain("wrong category");
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task IncrementUsageIfCodeMatchesAsync_WhenFreeTextReason_ShouldNotChangeUsage()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateAsync(new CreateReasonCodeRequest("DAMAGE", "Damage", null, null, "ADJUSTMENT", true));

        await service.IncrementUsageIfCodeMatchesAsync("Forklift broke pallet", ReasonCategory.ADJUSTMENT);

        await using var verifyDb = fixture.CreateDbContext();
        var row = await verifyDb.AdjustmentReasonCodes.SingleAsync(x => x.Code == "DAMAGE");
        row.UsageCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "ReasonCodes")]
    public async Task UpdateAsync_WhenValid_ShouldPersistChanges()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = (await service.CreateAsync(new CreateReasonCodeRequest(
            "RETURN",
            "Return",
            null,
            null,
            "RETURN",
            true))).Value;

        var result = await service.UpdateAsync(created.Id, new UpdateReasonCodeRequest(
            "RETURN-CUSTOMER",
            "Customer Return",
            "Updated",
            null,
            "RETURN",
            false));

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("RETURN-CUSTOMER");
        result.Value.Active.Should().BeFalse();
    }

    private sealed class TestFixture
    {
        private readonly string _databaseName = $"reason-codes-tests-{Guid.NewGuid():N}";
        private readonly ICurrentUserService _currentUserService = new TestCurrentUserService();

        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;

            return new WarehouseDbContext(options, _currentUserService);
        }

        public ReasonCodeService CreateService(WarehouseDbContext db)
        {
            return new ReasonCodeService(db, NullLoggerFactory.Instance.CreateLogger<ReasonCodeService>());
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "test-user";
    }
}
