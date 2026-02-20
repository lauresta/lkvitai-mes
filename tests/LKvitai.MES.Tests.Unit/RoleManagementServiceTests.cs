using FluentAssertions;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class RoleManagementServiceTests
{
    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenValid_ShouldSucceed()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            "Inventory Clerk",
            "Read-only access",
            [new RolePermissionRequest("ITEM", "READ")])) ;

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Inventory Clerk");
        result.Value.Permissions.Should().ContainSingle(x => x.Resource == "ITEM" && x.Action == "READ");
        result.Value.IsSystemRole.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenNameMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            " ",
            "invalid",
            [new RolePermissionRequest("ITEM", "READ")])) ;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("Role name is required");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenPermissionsMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateRoleAsync(new CreateRoleRequest("NoPermissions", null, []));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("At least 1 permission");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenPermissionResourceMissing_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            "BadPermission",
            null,
            [new RolePermissionRequest("", "READ")])) ;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("resource and action are required");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenPermissionNotPredefined_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            "BadPermission",
            null,
            [new RolePermissionRequest("ITEM", "DELETE")])) ;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("is not predefined");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAsync_WhenNameDuplicateIgnoringCase_ShouldFail()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        _ = await service.CreateRoleAsync(new CreateRoleRequest("Inventory Clerk", null, [new RolePermissionRequest("ITEM", "READ")])) ;
        var result = await service.CreateRoleAsync(new CreateRoleRequest("inventory clerk", null, [new RolePermissionRequest("LOCATION", "READ")])) ;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Contain("already exists");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task UpdateRoleAsync_WhenRoleMissing_ShouldReturnNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.UpdateRoleAsync(9999, new UpdateRoleRequest("Role", null, [new RolePermissionRequest("ITEM", "READ")])) ;

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task UpdateRoleAsync_WhenValid_ShouldReplacePermissions()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateRoleAsync(new CreateRoleRequest(
            "Picker",
            null,
            [new RolePermissionRequest("ITEM", "READ")])) ;

        var result = await service.UpdateRoleAsync(
            created.Value.Id,
            new UpdateRoleRequest(
                "Picker",
                "updated",
                [new RolePermissionRequest("ORDER", "UPDATE")])) ;

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("updated");
        result.Value.Permissions.Should().ContainSingle(x => x.Resource == "ORDER" && x.Action == "UPDATE");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task DeleteRoleAsync_WhenRoleMissing_ShouldReturnNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.DeleteRoleAsync(98765);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task DeleteRoleAsync_WhenSystemRole_ShouldFailValidation()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.DeleteRoleAsync(1);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.ErrorDetail.Should().Be("Cannot delete system role");
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task DeleteRoleAsync_WhenCustomRole_ShouldRemove()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateRoleAsync(new CreateRoleRequest(
            "Disposable",
            null,
            [new RolePermissionRequest("ITEM", "READ")])) ;

        var deleted = await service.DeleteRoleAsync(created.Value.Id);

        deleted.IsSuccess.Should().BeTrue();

        await using var verifyDb = fixture.CreateDbContext();
        var exists = await verifyDb.Roles.AnyAsync(x => x.Id == created.Value.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task AssignRoleAsync_WhenUserMissing_ShouldReturnNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.AssignRoleAsync(Guid.NewGuid(), 1, "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task AssignRoleAsync_WhenRoleMissing_ShouldReturnNotFound()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var result = await service.AssignRoleAsync(fixture.KnownUserId, 9999, "admin");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task AssignRoleAsync_WhenValid_ShouldPersistAssignment()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateRoleAsync(new CreateRoleRequest(
            "Inventory Read",
            null,
            [new RolePermissionRequest("ITEM", "READ")])) ;

        var result = await service.AssignRoleAsync(fixture.KnownUserId, created.Value.Id, "admin-user");

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(fixture.KnownUserId);
        result.Value.RoleId.Should().Be(created.Value.Id);
        result.Value.AssignedBy.Should().Be("admin-user");

        await using var verifyDb = fixture.CreateDbContext();
        var exists = await verifyDb.UserRoleAssignments.AnyAsync(x => x.UserId == fixture.KnownUserId && x.RoleId == created.Value.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task AssignRoleAsync_WhenCalledTwice_ShouldNotDuplicateRows()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var created = await service.CreateRoleAsync(new CreateRoleRequest("Dedup", null, [new RolePermissionRequest("ITEM", "READ")])) ;

        _ = await service.AssignRoleAsync(fixture.KnownUserId, created.Value.Id, "admin");
        _ = await service.AssignRoleAsync(fixture.KnownUserId, created.Value.Id, "admin");

        await using var verifyDb = fixture.CreateDbContext();
        var count = await verifyDb.UserRoleAssignments.CountAsync(x => x.UserId == fixture.KnownUserId && x.RoleId == created.Value.Id);
        count.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task HasPermissionAsync_WhenPermissionAssigned_ShouldReturnTrue()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("Reader", null, [new RolePermissionRequest("ITEM", "READ")])) ;
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var allowed = await service.HasPermissionAsync(fixture.KnownUserId, "ITEM", "READ");

        allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task HasPermissionAsync_WhenScopeOwnRequestedAndAllAssigned_ShouldReturnTrue()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("Reader", null, [new RolePermissionRequest("ITEM", "READ", "ALL")])) ;
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var allowed = await service.HasPermissionAsync(fixture.KnownUserId, "ITEM", "READ", "OWN");

        allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task HasPermissionAsync_WhenRoleAssignedAfterCacheWarmup_ShouldReflectImmediately()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var beforeAssignment = await service.HasPermissionAsync(fixture.KnownUserId, "ITEM", "READ");
        beforeAssignment.Should().BeFalse();

        var role = await service.CreateRoleAsync(new CreateRoleRequest("Reader", null, [new RolePermissionRequest("ITEM", "READ")])) ;
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var afterAssignment = await service.HasPermissionAsync(fixture.KnownUserId, "ITEM", "READ");

        afterAssignment.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task HasPermissionAsync_WhenRoleUpdatedAfterCacheWarmup_ShouldReflectImmediately()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("Updater", null, [new RolePermissionRequest("ITEM", "READ")])) ;
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var before = await service.HasPermissionAsync(fixture.KnownUserId, "ORDER", "UPDATE");
        before.Should().BeFalse();

        _ = await service.UpdateRoleAsync(
            role.Value.Id,
            new UpdateRoleRequest("Updater", null, [new RolePermissionRequest("ORDER", "UPDATE")])) ;

        var after = await service.HasPermissionAsync(fixture.KnownUserId, "ORDER", "UPDATE");

        after.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task GetPermissionsAsync_ShouldReturnAllAndOwnScopes()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var permissions = await service.GetPermissionsAsync();

        permissions.Should().Contain(x => x.Resource == "ITEM" && x.Action == "READ" && x.Scope == "ALL");
        permissions.Should().Contain(x => x.Resource == "ITEM" && x.Action == "READ" && x.Scope == "OWN");
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenOwnerMatchesAndOwnPermissionAssigned_ShouldReturnTrue()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("OwnOrderUpdater", null, [new RolePermissionRequest("ORDER", "UPDATE", "OWN")]));
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var allowed = await service.CheckPermissionAsync(
            fixture.KnownUserId,
            "ORDER",
            "UPDATE",
            fixture.KnownUserId);

        allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenOwnerDifferentAndOnlyOwnPermissionAssigned_ShouldReturnFalse()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("OwnOrderUpdater", null, [new RolePermissionRequest("ORDER", "UPDATE", "OWN")]));
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var allowed = await service.CheckPermissionAsync(
            fixture.KnownUserId,
            "ORDER",
            "UPDATE",
            Guid.NewGuid());

        allowed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenOwnerDifferentAndAllPermissionAssigned_ShouldReturnTrue()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("AllOrderUpdater", null, [new RolePermissionRequest("ORDER", "UPDATE", "ALL")]));
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var allowed = await service.CheckPermissionAsync(
            fixture.KnownUserId,
            "ORDER",
            "UPDATE",
            Guid.NewGuid());

        allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task CheckPermissionAsync_WhenResourceMissing_ShouldReturnFalse()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var allowed = await service.CheckPermissionAsync(fixture.KnownUserId, string.Empty, "READ");

        allowed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task HasAnyRoleAssignmentsAsync_WhenNone_ShouldReturnFalse()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var hasAssignments = await service.HasAnyRoleAssignmentsAsync(fixture.KnownUserId);

        hasAssignments.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task HasAnyRoleAssignmentsAsync_WhenAssigned_ShouldReturnTrue()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var role = await service.CreateRoleAsync(new CreateRoleRequest("Reader", null, [new RolePermissionRequest("ITEM", "READ", "ALL")]));
        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var hasAssignments = await service.HasAnyRoleAssignmentsAsync(fixture.KnownUserId);

        hasAssignments.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task HasPermissionAsync_WhenPermissionsAcrossMultipleRoles_ShouldAggregate()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);

        var roleA = await service.CreateRoleAsync(new CreateRoleRequest("ItemReader", null, [new RolePermissionRequest("ITEM", "READ", "ALL")]));
        var roleB = await service.CreateRoleAsync(new CreateRoleRequest("QcUpdater", null, [new RolePermissionRequest("QC", "UPDATE", "ALL")]));
        _ = await service.AssignRoleAsync(fixture.KnownUserId, roleA.Value.Id, "admin");
        _ = await service.AssignRoleAsync(fixture.KnownUserId, roleB.Value.Id, "admin");

        var canReadItem = await service.HasPermissionAsync(fixture.KnownUserId, "ITEM", "READ");
        var canUpdateQc = await service.HasPermissionAsync(fixture.KnownUserId, "QC", "UPDATE");

        canReadItem.Should().BeTrue();
        canUpdateQc.Should().BeTrue();
    }

    private sealed class TestFixture
    {
        private readonly string _databaseName = $"role-management-tests-{Guid.NewGuid():N}";
        private readonly ICurrentUserService _currentUserService = new TestCurrentUserService();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly StubAdminUserStore _adminUserStore;

        public TestFixture()
        {
            _adminUserStore = new StubAdminUserStore(
                new AdminUserView(
                    Guid.Parse("00000000-0000-0000-0000-000000000105"),
                    "warehouse-admin",
                    "admin@example.com",
                    [WarehouseRoles.WarehouseAdmin],
                    "Active",
                    DateTimeOffset.UtcNow,
                    null));
        }

        public Guid KnownUserId => _adminUserStore.KnownUserId;

        public WarehouseDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;

            var db = new WarehouseDbContext(options, _currentUserService);
            db.Database.EnsureCreated();
            return db;
        }

        public RoleManagementService CreateService(WarehouseDbContext db)
        {
            return new RoleManagementService(
                db,
                _adminUserStore,
                _cache,
                NullLoggerFactory.Instance.CreateLogger<RoleManagementService>());
        }
    }

    private sealed class StubAdminUserStore : IAdminUserStore
    {
        private readonly IReadOnlyList<AdminUserView> _users;

        public StubAdminUserStore(AdminUserView user)
        {
            _users = [user];
            KnownUserId = user.Id;
        }

        public Guid KnownUserId { get; }

        public IReadOnlyList<AdminUserView> GetAll() => _users;

        public bool TryCreate(CreateAdminUserRequest request, out AdminUserView? user, out string? error)
        {
            user = null;
            error = "Not implemented";
            return false;
        }

        public bool TryUpdate(Guid id, UpdateAdminUserRequest request, out AdminUserView? user, out string? error)
        {
            user = null;
            error = "Not implemented";
            return false;
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "test-user";
    }
}
