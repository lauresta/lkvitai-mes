using LKvitai.MES.Api.Security;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Api.Services;

public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionCatalogDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<Result<RoleDto>> UpdateRoleAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task<Result<UserRoleAssignmentDto>> AssignRoleAsync(Guid userId, int roleId, string assignedBy, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, string scope = "ALL", CancellationToken cancellationToken = default);
    Task<bool> CheckPermissionAsync(Guid userId, string resource, string action, Guid? ownerUserId = null, CancellationToken cancellationToken = default);
    Task<bool> HasAnyRoleAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class RoleManagementService : IRoleManagementService
{
    private static long _permissionCacheGeneration;

    private readonly WarehouseDbContext _dbContext;
    private readonly IAdminUserStore _adminUserStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        WarehouseDbContext dbContext,
        IAdminUserStore adminUserStore,
        IMemoryCache memoryCache,
        ILogger<RoleManagementService> logger)
    {
        _dbContext = dbContext;
        _adminUserStore = adminUserStore;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PermissionCatalogDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Resource)
            .ThenBy(x => x.Action)
            .ThenBy(x => x.Scope)
            .Select(x => new PermissionCatalogDto(x.Id, x.Resource, x.Action, x.Scope))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<RoleDto>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRoleRequestAsync(request.Name, request.Permissions, null, cancellationToken);
        if (!validation.IsSuccess)
        {
            return Result<RoleDto>.Fail(validation.ErrorCode ?? DomainErrorCodes.ValidationError, validation.ErrorDetail ?? validation.Error);
        }

        var permissionIds = await ResolvePermissionIdsAsync(request.Permissions, cancellationToken);
        if (!permissionIds.IsSuccess)
        {
            return Result<RoleDto>.Fail(permissionIds.ErrorCode ?? DomainErrorCodes.ValidationError, permissionIds.ErrorDetail ?? permissionIds.Error);
        }

        var role = new Role
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSystemRole = false
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var permissionId in permissionIds.Value)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidatePermissionCache();

        _logger.LogInformation("Role created: Id={RoleId}, Name={RoleName}, Permissions={PermissionCount}", role.Id, role.Name, permissionIds.Value.Count);

        var created = await LoadRoleAsync(role.Id, cancellationToken);
        return Result<RoleDto>.Ok(Map(created!));
    }

    public async Task<Result<RoleDto>> UpdateRoleAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Roles
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

        if (role is null)
        {
            return Result<RoleDto>.Fail(DomainErrorCodes.NotFound, $"Role '{roleId}' does not exist.");
        }

        var validation = await ValidateRoleRequestAsync(request.Name, request.Permissions, roleId, cancellationToken);
        if (!validation.IsSuccess)
        {
            return Result<RoleDto>.Fail(validation.ErrorCode ?? DomainErrorCodes.ValidationError, validation.ErrorDetail ?? validation.Error);
        }

        var permissionIds = await ResolvePermissionIdsAsync(request.Permissions, cancellationToken);
        if (!permissionIds.IsSuccess)
        {
            return Result<RoleDto>.Fail(permissionIds.ErrorCode ?? DomainErrorCodes.ValidationError, permissionIds.ErrorDetail ?? permissionIds.Error);
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();

        _dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var permissionId in permissionIds.Value)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidatePermissionCache();

        _logger.LogInformation("Role updated: Id={RoleId}, Name={RoleName}, Permissions={PermissionCount}", role.Id, role.Name, permissionIds.Value.Count);

        var updated = await LoadRoleAsync(role.Id, cancellationToken);
        return Result<RoleDto>.Ok(Map(updated!));
    }

    public async Task<Result> DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        if (role is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Role '{roleId}' does not exist.");
        }

        if (role.IsSystemRole)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cannot delete system role");
        }

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidatePermissionCache();

        _logger.LogInformation("Role deleted: Id={RoleId}, Name={RoleName}", role.Id, role.Name);
        return Result.Ok();
    }

    public async Task<Result<UserRoleAssignmentDto>> AssignRoleAsync(
        Guid userId,
        int roleId,
        string assignedBy,
        CancellationToken cancellationToken = default)
    {
        var userExists = _adminUserStore.GetAll().Any(x => x.Id == userId);
        if (!userExists)
        {
            return Result<UserRoleAssignmentDto>.Fail(DomainErrorCodes.NotFound, $"User '{userId}' does not exist.");
        }

        var role = await _dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        if (role is null)
        {
            return Result<UserRoleAssignmentDto>.Fail(DomainErrorCodes.NotFound, $"Role '{roleId}' does not exist.");
        }

        var existing = await _dbContext.UserRoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);

        if (existing is null)
        {
            _dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedBy = string.IsNullOrWhiteSpace(assignedBy) ? "system" : assignedBy.Trim()
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        InvalidatePermissionCache();

        var assignment = await _dbContext.UserRoleAssignments
            .AsNoTracking()
            .FirstAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);

        _logger.LogInformation("Role assigned: UserId={UserId}, RoleId={RoleId}", userId, roleId);

        return Result<UserRoleAssignmentDto>.Ok(new UserRoleAssignmentDto(
            assignment.UserId,
            assignment.RoleId,
            assignment.AssignedAt,
            assignment.AssignedBy));
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string resource,
        string action,
        string scope = "ALL",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "ALL" : scope.Trim().ToUpperInvariant();
        var normalizedToken = BuildPermissionToken(resource, action, normalizedScope);
        var cacheKey = $"role-management:user-permissions:{Interlocked.Read(ref _permissionCacheGeneration)}:{userId}";

        if (!_memoryCache.TryGetValue(cacheKey, out HashSet<string>? permissions) || permissions is null)
        {
            permissions = await LoadUserPermissionTokensAsync(userId, cancellationToken);
            _memoryCache.Set(cacheKey, permissions, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });
        }

        if (permissions.Contains(normalizedToken))
        {
            return true;
        }

        return !string.Equals(normalizedScope, "ALL", StringComparison.OrdinalIgnoreCase) &&
               permissions.Contains(BuildPermissionToken(resource, action, "ALL"));
    }

    public async Task<bool> CheckPermissionAsync(
        Guid userId,
        string resource,
        string action,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resource) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        if (ownerUserId.HasValue && ownerUserId.Value != userId)
        {
            return await HasPermissionAsync(userId, resource, action, "ALL", cancellationToken);
        }

        return await HasPermissionAsync(userId, resource, action, "OWN", cancellationToken);
    }

    public async Task<bool> HasAnyRoleAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserRoleAssignments
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId, cancellationToken);
    }

    private async Task<Result> ValidateRoleRequestAsync(
        string name,
        IReadOnlyList<RolePermissionRequest> permissions,
        int? roleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Role name is required.");
        }

        if (permissions is null || permissions.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least 1 permission is required per role.");
        }

        var normalizedName = name.Trim();
        var duplicate = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != roleId &&
                     x.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, $"Role '{normalizedName}' already exists.");
        }

        return Result.Ok();
    }

    private async Task<Result<List<int>>> ResolvePermissionIdsAsync(
        IReadOnlyList<RolePermissionRequest> permissions,
        CancellationToken cancellationToken)
    {
        if (permissions is null || permissions.Count == 0)
        {
            return Result<List<int>>.Fail(DomainErrorCodes.ValidationError, "At least 1 permission is required per role.");
        }

        var normalized = permissions
            .Where(x => x is not null)
            .Select(x => new
            {
                Resource = (x.Resource ?? string.Empty).Trim().ToUpperInvariant(),
                Action = (x.Action ?? string.Empty).Trim().ToUpperInvariant(),
                Scope = string.IsNullOrWhiteSpace(x.Scope) ? "ALL" : x.Scope.Trim().ToUpperInvariant()
            })
            .Distinct()
            .ToList();

        if (normalized.Any(x => string.IsNullOrWhiteSpace(x.Resource) || string.IsNullOrWhiteSpace(x.Action)))
        {
            return Result<List<int>>.Fail(
                DomainErrorCodes.ValidationError,
                "Permission resource and action are required.");
        }

        var available = await _dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var ids = new List<int>();
        foreach (var permission in normalized)
        {
            var match = available.FirstOrDefault(x =>
                string.Equals(x.Resource, permission.Resource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Action, permission.Action, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Scope, permission.Scope, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return Result<List<int>>.Fail(
                    DomainErrorCodes.ValidationError,
                    $"Permission '{permission.Resource}:{permission.Action}:{permission.Scope}' is not predefined.");
            }

            ids.Add(match.Id);
        }

        return Result<List<int>>.Ok(ids);
    }

    private async Task<Role?> LoadRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);
    }

    private async Task<HashSet<string>> LoadUserPermissionTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(
                _dbContext.RolePermissions.AsNoTracking(),
                assignment => assignment.RoleId,
                rolePermission => rolePermission.RoleId,
                (_, rolePermission) => rolePermission.PermissionId)
            .Join(
                _dbContext.Permissions.AsNoTracking(),
                permissionId => permissionId,
                permission => permission.Id,
                (_, permission) => BuildPermissionToken(permission.Resource, permission.Action, permission.Scope))
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
    }

    private void InvalidatePermissionCache()
    {
        Interlocked.Increment(ref _permissionCacheGeneration);
    }

    private static RoleDto Map(Role role)
    {
        var permissions = role.RolePermissions
            .Where(x => x.Permission is not null)
            .Select(x => new RolePermissionDto(
                x.PermissionId,
                x.Permission!.Resource,
                x.Permission.Action,
                x.Permission.Scope))
            .OrderBy(x => x.Resource)
            .ThenBy(x => x.Action)
            .ThenBy(x => x.Scope)
            .ToList();

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            permissions,
            role.CreatedBy,
            role.CreatedAt,
            role.UpdatedBy,
            role.UpdatedAt);
    }

    private static string BuildPermissionToken(string resource, string action, string scope)
        => $"{resource.Trim().ToUpperInvariant()}|{action.Trim().ToUpperInvariant()}|{scope.Trim().ToUpperInvariant()}";
}

public sealed record RoleDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<RolePermissionDto> Permissions,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

public sealed record RolePermissionDto(int Id, string Resource, string Action, string Scope);
public sealed record PermissionCatalogDto(int Id, string Resource, string Action, string Scope);

public sealed record RolePermissionRequest(string Resource, string Action, string Scope = "ALL");

public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<RolePermissionRequest> Permissions);

public sealed record UpdateRoleRequest(string Name, string? Description, IReadOnlyList<RolePermissionRequest> Permissions);

public sealed record UserRoleAssignmentDto(Guid UserId, int RoleId, DateTimeOffset AssignedAt, string AssignedBy);
