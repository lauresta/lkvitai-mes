using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class OAuthRoleMapperTests
{
    private readonly OAuthRoleMapper _sut = new();

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenMappedGroupClaim_ShouldUseConfiguredRole()
    {
        var options = CreateOptions();
        var claims = new[] { new Claim("groups", "Warehouse-Managers") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain(WarehouseRoles.WarehouseManager);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenConfiguredRoleIsManagerAlias_ShouldAddWarehouseManagerAlias()
    {
        var options = CreateOptions();
        options.RoleMappings["Ops-Managers"] = "Manager";
        var claims = new[] { new Claim("groups", "Ops-Managers") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain("Manager");
        roles.Should().Contain(WarehouseRoles.WarehouseManager);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenRoleClaimProvided_ShouldIncludeRole()
    {
        var options = CreateOptions();
        var claims = new[] { new Claim(ClaimTypes.Role, WarehouseRoles.DispatchClerk) };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain(WarehouseRoles.DispatchClerk);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenCustomRoleClaimTypeConfigured_ShouldReadFromConfiguredClaimType()
    {
        var options = CreateOptions();
        options.RoleClaimType = "custom-groups";
        var claims = new[] { new Claim("custom-groups", "Warehouse-Admins") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain(WarehouseRoles.WarehouseAdmin);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenNoRoleClaims_ShouldUseDefaultRole()
    {
        var options = CreateOptions();
        options.DefaultRole = WarehouseRoles.Operator;

        var roles = _sut.MapRoles(Array.Empty<Claim>(), options);

        roles.Should().Equal(WarehouseRoles.Operator);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenMappingKeysDifferByCase_ShouldMapCaseInsensitive()
    {
        var options = CreateOptions();
        var claims = new[] { new Claim("groups", "warehouse-managers") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain(WarehouseRoles.WarehouseManager);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenDuplicateClaims_ShouldDeduplicate()
    {
        var options = CreateOptions();
        var claims = new[]
        {
            new Claim("groups", "Warehouse-Managers"),
            new Claim("groups", "Warehouse-Managers"),
            new Claim(ClaimTypes.Role, WarehouseRoles.WarehouseManager)
        };

        var roles = _sut.MapRoles(claims, options);

        roles.Count(x => x == WarehouseRoles.WarehouseManager).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenAdminAliasClaim_ShouldAddWarehouseAdminAlias()
    {
        var options = CreateOptions();
        var claims = new[] { new Claim("role", "Admin") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain("Admin");
        roles.Should().Contain(WarehouseRoles.WarehouseAdmin);
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenUnmappedRoleClaim_ShouldPreserveOriginalRole()
    {
        var options = CreateOptions();
        var claims = new[] { new Claim("groups", "Custom-Auditor") };

        var roles = _sut.MapRoles(claims, options);

        roles.Should().Contain("Custom-Auditor");
    }

    [Fact]
    [Trait("Category", "OAuth")]
    public void MapRoles_WhenDefaultRoleEmptyAndNoClaims_ShouldReturnEmpty()
    {
        var options = CreateOptions();
        options.DefaultRole = string.Empty;

        var roles = _sut.MapRoles(Array.Empty<Claim>(), options);

        roles.Should().BeEmpty();
    }

    private static OAuthOptions CreateOptions()
    {
        return new OAuthOptions
        {
            RoleClaimType = "groups",
            DefaultRole = WarehouseRoles.Operator,
            RoleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warehouse-Managers"] = WarehouseRoles.WarehouseManager,
                ["Warehouse-Admins"] = WarehouseRoles.WarehouseAdmin
            }
        };
    }
}
