using System.Collections.Concurrent;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;

namespace LKvitai.MES.Api.Security;

public sealed record AdminUserView(
    Guid Id,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class AdminUserRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed record CreateAdminUserRequest(
    string Username,
    string Email,
    string Password,
    IReadOnlyList<string> Roles,
    string Status);

public sealed record UpdateAdminUserRequest(
    IReadOnlyList<string> Roles,
    string Status,
    string? Email = null);

public interface IAdminUserStore
{
    IReadOnlyList<AdminUserView> GetAll();
    bool TryCreate(CreateAdminUserRequest request, out AdminUserView? user, out string? error);
    bool TryUpdate(Guid id, UpdateAdminUserRequest request, out AdminUserView? user, out string? error);
}

public sealed class InMemoryAdminUserStore : IAdminUserStore
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        WarehouseRoles.WarehouseAdmin,
        WarehouseRoles.WarehouseManager,
        WarehouseRoles.Operator,
        WarehouseRoles.QCInspector,
        WarehouseRoles.SalesAdmin,
        WarehouseRoles.PackingOperator,
        WarehouseRoles.DispatchClerk,
        WarehouseRoles.InventoryAccountant,
        WarehouseRoles.CFO
    };

    private readonly ConcurrentDictionary<Guid, AdminUserRecord> _users = new();
    private readonly PasswordHasher<AdminUserRecord> _passwordHasher = new();

    public InMemoryAdminUserStore()
    {
        var admin = new AdminUserRecord
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@example.com",
            Roles = [WarehouseRoles.WarehouseAdmin, WarehouseRoles.WarehouseManager],
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, "Admin123!");
        _users.TryAdd(admin.Id, admin);
    }

    public IReadOnlyList<AdminUserView> GetAll()
    {
        return _users.Values
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(MapView)
            .ToList();
    }

    public bool TryCreate(CreateAdminUserRequest request, out AdminUserView? user, out string? error)
    {
        user = null;
        error = ValidateCommon(request.Username, request.Email, request.Roles, request.Status, request.Password);
        if (error is not null)
        {
            return false;
        }

        if (_users.Values.Any(x => string.Equals(x.Username, request.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            error = "Username must be unique.";
            return false;
        }

        var normalizedRoles = NormalizeRoles(request.Roles);
        var normalizedStatus = NormalizeStatus(request.Status);
        var model = new AdminUserRecord
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            Roles = normalizedRoles,
            Status = normalizedStatus,
            CreatedAt = DateTimeOffset.UtcNow
        };
        model.PasswordHash = _passwordHasher.HashPassword(model, request.Password.Trim());

        if (!_users.TryAdd(model.Id, model))
        {
            error = "Failed to create user.";
            return false;
        }

        user = MapView(model);
        return true;
    }

    public bool TryUpdate(Guid id, UpdateAdminUserRequest request, out AdminUserView? user, out string? error)
    {
        user = null;
        error = ValidateUpdate(request.Roles, request.Status, request.Email);
        if (error is not null)
        {
            return false;
        }

        if (!_users.TryGetValue(id, out var existing))
        {
            error = "User not found.";
            return false;
        }

        existing.Roles = NormalizeRoles(request.Roles);
        existing.Status = NormalizeStatus(request.Status);
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            existing.Email = request.Email.Trim();
        }

        existing.UpdatedAt = DateTimeOffset.UtcNow;
        user = MapView(existing);
        return true;
    }

    private static string? ValidateCommon(
        string username,
        string email,
        IReadOnlyList<string> roles,
        string status,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username required.";
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email required.";
        }

        if (!IsValidEmail(email))
        {
            return "Invalid email format.";
        }

        if (roles.Count == 0)
        {
            return "At least one role is required.";
        }

        if (!roles.All(IsAllowedRole))
        {
            return "One or more roles are invalid.";
        }

        if (!IsAllowedStatus(status))
        {
            return "Status must be Active or Inactive.";
        }

        if (password is null)
        {
            return null;
        }

        if (password.Trim().Length < 8)
        {
            return "Password must be at least 8 characters.";
        }

        return null;
    }

    private static string? ValidateUpdate(IReadOnlyList<string> roles, string status, string? email)
    {
        if (roles.Count == 0)
        {
            return "At least one role is required.";
        }

        if (!roles.All(IsAllowedRole))
        {
            return "One or more roles are invalid.";
        }

        if (!IsAllowedStatus(status))
        {
            return "Status must be Active or Inactive.";
        }

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            return "Invalid email format.";
        }

        return null;
    }

    private static bool IsAllowedRole(string role)
        => AllowedRoles.Contains(role.Trim());

    private static bool IsAllowedStatus(string status)
        => string.Equals(status.Trim(), "Active", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.Trim(), "Inactive", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> NormalizeRoles(IReadOnlyList<string> roles)
    {
        return roles
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeStatus(string status)
        => string.Equals(status.Trim(), "Inactive", StringComparison.OrdinalIgnoreCase)
            ? "Inactive"
            : "Active";

    private static AdminUserView MapView(AdminUserRecord user)
        => new(
            user.Id,
            user.Username,
            user.Email,
            user.Roles.AsReadOnly(),
            user.Status,
            user.CreatedAt,
            user.UpdatedAt);
}
