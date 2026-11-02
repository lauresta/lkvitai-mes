using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class DatabaseAdminUserStoreTests
{
    private static DbContextOptions<WarehouseDbContext> Options() => new DbContextOptionsBuilder<WarehouseDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static CreateAdminUserRequest NewUser(string username = "operator")
        => new(username, username + "@example.test", "OnlyForTests123!", [WarehouseRoles.WarehouseAdmin, WarehouseRoles.WarehouseManager], "Active", @"TERMINAL\operator");

    [Fact]
    public void Records_password_hashes_and_explicit_mapping_survive_new_store_and_context()
    {
        var options = Options();
        Guid id;
        using (var context = new WarehouseDbContext(options))
        {
            var store = new DatabaseAdminUserStore(context);
            Assert.True(store.TryCreate(NewUser(), out var created, out _));
            id = created!.Id;
            var record = context.LocalUserAccounts.Single();
            Assert.NotEqual("OnlyForTests123!", record.PasswordHash);
            Assert.DoesNotContain("OnlyForTests123!", record.PasswordHash);
            Assert.DoesNotContain(record.PasswordHash, JsonSerializer.Serialize(created));
        }
        using (var restarted = new WarehouseDbContext(options))
        {
            var store = new DatabaseAdminUserStore(restarted);
            Assert.True(store.TryValidatePassword("OPERATOR", "OnlyForTests123!", out var validated));
            Assert.Equal(id, validated!.Id);
            Assert.Equal([WarehouseRoles.WarehouseAdmin, WarehouseRoles.WarehouseManager], validated.Roles);
            Assert.True(store.TryGetLegacySqlLogin(id, out var mapping));
            Assert.Equal(@"TERMINAL\operator", mapping);
            Assert.False(store.TryValidatePassword("operator", "wrong-password", out _));
        }
    }

    [Fact]
    public void Imported_identity_guid_roles_and_old_password_hash_remain_compatible()
    {
        var options = Options();
        var priorRecord = new AdminUserRecord { Id = Guid.NewGuid(), Username = "admin", Roles = [WarehouseRoles.WarehouseAdmin] };
        priorRecord.PasswordHash = new PasswordHasher<AdminUserRecord>().HashPassword(priorRecord, "LegacyTestPassword!");
        using (var context = new WarehouseDbContext(options))
        {
            context.LocalUserAccounts.Add(new LocalUserAccount
            {
                Id = priorRecord.Id, Username = "admin", NormalizedUsername = "ADMIN", Email = "admin@example.test",
                Roles = priorRecord.Roles, PasswordHash = priorRecord.PasswordHash, Status = "Active", CreatedAt = DateTimeOffset.UtcNow, LegacySqlLogin = "sa"
            });
            context.SaveChanges();
        }
        using var restarted = new WarehouseDbContext(options);
        var store = new DatabaseAdminUserStore(restarted);
        Assert.True(store.TryValidatePassword("admin", "LegacyTestPassword!", out var user));
        Assert.Equal(priorRecord.Id, user!.Id);
        Assert.Equal(priorRecord.Roles, user.Roles);
        Assert.True(store.TryGetLegacySqlLogin(priorRecord.Id, out var mapping));
        Assert.Equal("sa", mapping);
    }

    [Fact]
    public void Mapping_requires_explicit_update_intent_and_can_be_explicitly_cleared()
    {
        using var context = new WarehouseDbContext(Options());
        var store = new DatabaseAdminUserStore(context);
        Assert.True(store.TryCreate(NewUser(), out var user, out _));
        Assert.True(store.TryUpdate(user!.Id, new UpdateAdminUserRequest([WarehouseRoles.Operator], "Active"), out var roleChange, out _));
        Assert.Equal(@"TERMINAL\operator", roleChange!.LegacySqlLogin);
        Assert.True(store.TryUpdate(user.Id, new UpdateAdminUserRequest([WarehouseRoles.Operator], "Active", LegacySqlLogin: "sa", UpdateLegacySqlLogin: true), out var changed, out _));
        Assert.Equal("sa", changed!.LegacySqlLogin);
        Assert.True(store.TryUpdate(user.Id, new UpdateAdminUserRequest([WarehouseRoles.Operator], "Active", LegacySqlLogin: "", UpdateLegacySqlLogin: true), out var cleared, out _));
        Assert.Null(cleared!.LegacySqlLogin);
        Assert.False(store.TryGetLegacySqlLogin(user.Id, out var mapping));
        Assert.Null(mapping);
    }

    [Fact]
    public void Inactive_or_unmapped_users_do_not_resolve_to_a_legacy_login()
    {
        using var context = new WarehouseDbContext(Options());
        var store = new DatabaseAdminUserStore(context);
        Assert.True(store.TryCreate(NewUser() with { LegacySqlLogin = null }, out var unmapped, out _));
        Assert.False(store.TryGetLegacySqlLogin(unmapped!.Id, out _));
        Assert.True(store.TryCreate(NewUser("inactive") with { Status = "Inactive" }, out var inactive, out _));
        Assert.False(store.TryGetLegacySqlLogin(inactive!.Id, out _));
        Assert.False(store.TryValidatePassword("inactive", "OnlyForTests123!", out _));
        Assert.False(store.TryGetLegacySqlLogin(Guid.NewGuid(), out _));
    }

    [Theory]
    [InlineData("sa; EXEC something")]
    [InlineData("sa\n")]
    [InlineData("[sa]")]
    [InlineData("'sa'")]
    public void Invalid_mapping_is_rejected_without_creating_a_record(string mapping)
    {
        using var context = new WarehouseDbContext(Options());
        var store = new DatabaseAdminUserStore(context);
        Assert.False(store.TryCreate(NewUser() with { LegacySqlLogin = mapping }, out _, out var error));
        Assert.Contains("Legacy SQL login", error);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Empty_database_does_not_silently_create_a_new_default_admin_identity()
    {
        using var context = new WarehouseDbContext(Options());
        var store = new DatabaseAdminUserStore(context);
        Assert.Empty(store.GetAll());
        Assert.False(store.TryValidatePassword("admin", "Admin123!", out _));
        Assert.True(store.TryCreate(NewUser(), out _, out _));
        Assert.False(store.TryCreate(NewUser("OPERATOR"), out _, out var error));
        Assert.Equal("Username must be unique.", error);
    }

    [Fact]
    public void Sso_username_lookup_returns_only_the_existing_active_durable_account()
    {
        using var context = new WarehouseDbContext(Options());
        var store = new DatabaseAdminUserStore(context);
        Assert.True(store.TryCreate(NewUser("bykovas"), out var created, out _));
        var linked = store.FindActiveByUsername("BYKOVAS");
        Assert.NotNull(linked);
        Assert.Equal(created!.Id, linked!.Id);
        Assert.Equal(created.LegacySqlLogin, linked.LegacySqlLogin);
        Assert.Null(store.FindActiveByUsername("missing-user"));
        Assert.Null(store.FindActiveByUsername(""));
        Assert.True(store.TryUpdate(created.Id, new UpdateAdminUserRequest(created.Roles, "Inactive"), out _, out _));
        Assert.Null(store.FindActiveByUsername("bykovas"));
        Assert.Single(store.GetAll()); // No auto-provisioning or duplicate account.
    }

    [Fact]
    public void User_management_remains_admin_only()
    {
        var authorize = Assert.Single(typeof(AdminUsersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(WarehousePolicies.AdminOnly, authorize.Policy);
    }
}
