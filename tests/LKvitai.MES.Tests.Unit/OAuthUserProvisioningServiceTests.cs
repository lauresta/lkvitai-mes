using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class OAuthUserProvisioningServiceTests
{
    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenUserDoesNotExist_ShouldCreateUser()
    {
        var store = new InMemoryAdminUserStore();
        var sut = CreateSut(store);
        var principal = CreatePrincipal("new-user", "new-user@example.com");

        var result = sut.Provision(principal, [WarehouseRoles.WarehouseManager]);

        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().Contain(WarehouseRoles.WarehouseManager);
        store.GetAll().Should().Contain(x => string.Equals(x.Username, "new-user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenUserExists_ShouldUpdateRoles()
    {
        var store = new InMemoryAdminUserStore();
        var existingId = store.GetAll().Single(x => x.Username == "admin").Id;
        var sut = CreateSut(store);
        var principal = CreatePrincipal("admin", "admin@example.com");

        var result = sut.Provision(principal, [WarehouseRoles.CFO]);

        result.IsSuccess.Should().BeTrue();
        result.UserId.Should().Be(existingId.ToString());
        store.GetAll().Single(x => x.Id == existingId).Roles.Should().Contain(WarehouseRoles.CFO);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenUsernameMissing_ShouldFail()
    {
        var store = new InMemoryAdminUserStore();
        var sut = CreateSut(store);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = sut.Provision(principal, [WarehouseRoles.Operator]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("username");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenRolesEmpty_ShouldUseOperatorFallback()
    {
        var store = new InMemoryAdminUserStore();
        var sut = CreateSut(store);
        var principal = CreatePrincipal("fallback-user", "fallback@example.com");

        var result = sut.Provision(principal, []);

        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().Contain(WarehouseRoles.Operator);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenEmailClaimMissing_ShouldGenerateFallbackEmail()
    {
        var store = new InMemoryAdminUserStore();
        var sut = CreateSut(store);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("preferred_username", "fallback-mail-user") }));

        var result = sut.Provision(principal, [WarehouseRoles.Operator]);

        result.IsSuccess.Should().BeTrue();
        store.GetAll().Should().Contain(x => x.Email == "fallback-mail-user@oauth.local");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void Provision_WhenStoreUpdateFails_ShouldReturnFailure()
    {
        var store = new FailingAdminUserStore();
        var sut = CreateSut(store);
        var principal = CreatePrincipal("existing-user", "existing@example.com");

        var result = sut.Provision(principal, [WarehouseRoles.Operator]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    private static OAuthUserProvisioningService CreateSut(IAdminUserStore store)
        => new(store, NullLoggerFactory.Instance.CreateLogger<OAuthUserProvisioningService>());

    private static ClaimsPrincipal CreatePrincipal(string username, string email)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", username),
            new Claim(ClaimTypes.Email, email),
            new Claim("sub", username)
        ]));
    }

    private sealed class FailingAdminUserStore : IAdminUserStore
    {
        public IReadOnlyList<AdminUserView> GetAll()
            =>
            [
                new AdminUserView(
                    Guid.Parse("00000000-0000-0000-0000-000000000501"),
                    "existing-user",
                    "existing@example.com",
                    [WarehouseRoles.Operator],
                    "Active",
                    DateTimeOffset.UtcNow,
                    null)
            ];

        public bool TryCreate(CreateAdminUserRequest request, out AdminUserView? user, out string? error)
        {
            user = null;
            error = "Create failed";
            return false;
        }

        public bool TryUpdate(Guid id, UpdateAdminUserRequest request, out AdminUserView? user, out string? error)
        {
            user = null;
            error = "Update failed";
            return false;
        }
    }
}
