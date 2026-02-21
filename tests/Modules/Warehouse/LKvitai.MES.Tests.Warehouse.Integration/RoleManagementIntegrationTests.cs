using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class RoleManagementIntegrationTests
{
    [Fact]
    [Trait("Category", "RoleManagement")]
    public async Task CreateRoleAssignUserThenVerifyPermissions_ShouldSucceed()
    {
        var dbName = $"role-management-integration-{Guid.NewGuid():N}";
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000205");
        var store = new StubAdminUserStore(userId);
        var currentUser = new TestCurrentUserService();

        await using (var db = CreateDbContext(dbName, currentUser))
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new RoleManagementService(
                db,
                store,
                cache,
                NullLoggerFactory.Instance.CreateLogger<RoleManagementService>());

            var created = await service.CreateRoleAsync(new CreateRoleRequest(
                "Inventory Clerk",
                "Read inventory and locations",
                [
                    new RolePermissionRequest("ITEM", "READ"),
                    new RolePermissionRequest("LOCATION", "READ")
                ]));

            created.IsSuccess.Should().BeTrue();

            var assigned = await service.AssignRoleAsync(userId, created.Value.Id, "integration-admin");

            assigned.IsSuccess.Should().BeTrue();
        }

        await using (var verifyDb = CreateDbContext(dbName, currentUser))
        {
            using var verifyCache = new MemoryCache(new MemoryCacheOptions());
            var verifyService = new RoleManagementService(
                verifyDb,
                store,
                verifyCache,
                NullLoggerFactory.Instance.CreateLogger<RoleManagementService>());

            var hasItemRead = await verifyService.HasPermissionAsync(userId, "ITEM", "READ");
            var hasLocationRead = await verifyService.HasPermissionAsync(userId, "LOCATION", "READ");
            var hasOrderUpdate = await verifyService.HasPermissionAsync(userId, "ORDER", "UPDATE");

            hasItemRead.Should().BeTrue();
            hasLocationRead.Should().BeTrue();
            hasOrderUpdate.Should().BeFalse();
        }
    }

    private static WarehouseDbContext CreateDbContext(string dbName, ICurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new WarehouseDbContext(options, currentUser);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class StubAdminUserStore : IAdminUserStore
    {
        private readonly IReadOnlyList<AdminUserView> _users;

        public StubAdminUserStore(Guid userId)
        {
            _users =
            [
                new AdminUserView(
                    userId,
                    "integration-user",
                    "integration-user@example.com",
                    [WarehouseRoles.WarehouseAdmin],
                    "Active",
                    DateTimeOffset.UtcNow,
                    null)
            ];
        }

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
        public string GetCurrentUserId() => "integration-user";
    }
}
