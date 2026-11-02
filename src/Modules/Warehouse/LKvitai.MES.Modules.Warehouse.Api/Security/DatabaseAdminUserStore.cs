using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

/// <summary>
/// Existing user-store contract backed by Warehouse PostgreSQL. No automatic
/// default-user creation: deployment imports existing identities explicitly.
/// Passwords are stored only as Identity password hashes, never returned in views.
/// </summary>
public sealed class DatabaseAdminUserStore(WarehouseDbContext database) : IAdminUserStore
{
    private readonly PasswordHasher<LocalUserAccount> _passwordHasher = new();

    public IReadOnlyList<AdminUserView> GetAll() => database.LocalUserAccounts.AsNoTracking().ToList()
        .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase).Select(MapView).ToArray();

    public bool TryCreate(CreateAdminUserRequest request, out AdminUserView? user, out string? error)
    {
        user = null;
        error = InMemoryAdminUserStore.ValidateCommon(request.Username, request.Email, request.Roles, request.Status, request.Password)
            ?? InMemoryAdminUserStore.ValidateLegacySqlLogin(request.LegacySqlLogin);
        if (error is not null) return false;
        var normalized = request.Username.Trim().ToUpperInvariant();
        if (database.LocalUserAccounts.Any(account => account.NormalizedUsername == normalized))
        {
            error = "Username must be unique.";
            return false;
        }
        var model = new LocalUserAccount
        {
            Id = Guid.NewGuid(), Username = request.Username.Trim(), NormalizedUsername = normalized,
            Email = request.Email.Trim(), Roles = InMemoryAdminUserStore.NormalizeRoles(request.Roles),
            Status = InMemoryAdminUserStore.NormalizeStatus(request.Status),
            LegacySqlLogin = InMemoryAdminUserStore.NormalizeLegacySqlLogin(request.LegacySqlLogin),
            CreatedAt = DateTimeOffset.UtcNow
        };
        model.PasswordHash = _passwordHasher.HashPassword(model, request.Password);
        database.LocalUserAccounts.Add(model);
        try { database.SaveChanges(); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            database.Entry(model).State = EntityState.Detached;
            error = "Username must be unique.";
            return false;
        }
        user = MapView(model);
        return true;
    }

    public bool TryUpdate(Guid id, UpdateAdminUserRequest request, out AdminUserView? user, out string? error)
    {
        user = null;
        error = InMemoryAdminUserStore.ValidateUpdate(request.Roles, request.Status, request.Email)
            ?? (request.UpdateLegacySqlLogin ? InMemoryAdminUserStore.ValidateLegacySqlLogin(request.LegacySqlLogin) : null);
        if (error is not null) return false;
        var model = database.LocalUserAccounts.SingleOrDefault(account => account.Id == id);
        if (model is null) { error = "User not found."; return false; }
        model.Roles = InMemoryAdminUserStore.NormalizeRoles(request.Roles);
        model.Status = InMemoryAdminUserStore.NormalizeStatus(request.Status);
        if (!string.IsNullOrWhiteSpace(request.Email)) model.Email = request.Email.Trim();
        if (request.UpdateLegacySqlLogin) model.LegacySqlLogin = InMemoryAdminUserStore.NormalizeLegacySqlLogin(request.LegacySqlLogin);
        model.UpdatedAt = DateTimeOffset.UtcNow;
        database.SaveChanges();
        user = MapView(model);
        return true;
    }

    public bool TryValidatePassword(string username, string password, out AdminUserView? user)
    {
        user = null;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;
        var normalized = username.Trim().ToUpperInvariant();
        var model = database.LocalUserAccounts.SingleOrDefault(account => account.NormalizedUsername == normalized);
        if (model is null || model.Status != AdminUserRecord.ActiveStatus) return false;
        var result = _passwordHasher.VerifyHashedPassword(model, model.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            model.PasswordHash = _passwordHasher.HashPassword(model, password);
            database.SaveChanges();
        }
        user = MapView(model);
        return true;
    }

    public bool TryGetLegacySqlLogin(Guid id, out string? legacySqlLogin)
    {
        legacySqlLogin = database.LocalUserAccounts.AsNoTracking()
            .Where(account => account.Id == id && account.Status == AdminUserRecord.ActiveStatus)
            .Select(account => account.LegacySqlLogin).SingleOrDefault();
        return !string.IsNullOrWhiteSpace(legacySqlLogin);
    }

    private static AdminUserView MapView(LocalUserAccount user) => new(user.Id, user.Username, user.Email,
        user.Roles.AsReadOnly(), user.Status, user.CreatedAt, user.UpdatedAt, user.LegacySqlLogin);
}
