using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class PermissionCheckIntegrationTests
{
    [Fact]
    [Trait("Category", "Permission")]
    public async Task AssignPermissionsThenCheck_ShouldRespectOwnVsAllScope()
    {
        var fixture = new TestFixture();
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateService(db);
        var controller = new AdminPermissionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var role = await service.CreateRoleAsync(new CreateRoleRequest(
            "OrderOwnUpdater",
            null,
            [new RolePermissionRequest("ORDER", "UPDATE", "OWN")]));

        _ = await service.AssignRoleAsync(fixture.KnownUserId, role.Value.Id, "admin");

        var ownResult = await controller.CheckPermissionAsync(new AdminPermissionsController.CheckPermissionPayload(
            fixture.KnownUserId,
            "ORDER",
            "UPDATE",
            fixture.KnownUserId));

        var otherResult = await controller.CheckPermissionAsync(new AdminPermissionsController.CheckPermissionPayload(
            fixture.KnownUserId,
            "ORDER",
            "UPDATE",
            Guid.NewGuid()));

        var ownPayload = ownResult.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AdminPermissionsController.PermissionCheckResponse>().Subject;
        var otherPayload = otherResult.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AdminPermissionsController.PermissionCheckResponse>().Subject;

        ownPayload.Allowed.Should().BeTrue();
        otherPayload.Allowed.Should().BeFalse();
    }

    private sealed class TestFixture
    {
        private readonly string _databaseName = $"permission-check-integration-{Guid.NewGuid():N}";
        private readonly ICurrentUserService _currentUserService = new TestCurrentUserService();
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly StubAdminUserStore _adminUserStore;

        public TestFixture()
        {
            _adminUserStore = new StubAdminUserStore(
                new AdminUserView(
                    Guid.Parse("00000000-0000-0000-0000-000000000305"),
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
                NullLogger<RoleManagementService>.Instance);
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
