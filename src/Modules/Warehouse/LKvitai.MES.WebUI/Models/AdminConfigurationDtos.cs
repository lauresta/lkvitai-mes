namespace LKvitai.MES.WebUI.Models;

public record WarehouseSettingsDto
{
    public int Id { get; init; }
    public int CapacityThresholdPercent { get; init; }
    public string DefaultPickStrategy { get; init; } = "FEFO";
    public int LowStockThreshold { get; init; }
    public int ReorderPoint { get; init; }
    public bool AutoAllocateOrders { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record UpdateWarehouseSettingsRequestDto
{
    public int CapacityThresholdPercent { get; init; }
    public string DefaultPickStrategy { get; init; } = "FEFO";
    public int LowStockThreshold { get; init; }
    public int ReorderPoint { get; init; }
    public bool AutoAllocateOrders { get; init; }
}

public record ReasonCodeDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? ParentId { get; init; }
    public string Category { get; init; } = string.Empty;
    public bool Active { get; init; }
    public int UsageCount { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record UpsertReasonCodeRequestDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? ParentId { get; init; }
    public string Category { get; init; } = "ADJUSTMENT";
    public bool Active { get; init; } = true;
}

public record ApprovalRuleDto
{
    public int Id { get; init; }
    public string RuleType { get; init; } = string.Empty;
    public string ThresholdType { get; init; } = string.Empty;
    public decimal ThresholdValue { get; init; }
    public string ApproverRole { get; init; } = string.Empty;
    public bool Active { get; init; }
    public int Priority { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record UpsertApprovalRuleRequestDto
{
    public string RuleType { get; init; } = "COST_ADJUSTMENT";
    public string ThresholdType { get; init; } = "AMOUNT";
    public decimal ThresholdValue { get; init; }
    public string ApproverRole { get; init; } = "WarehouseManager";
    public bool Active { get; init; } = true;
    public int Priority { get; init; } = 1;
}

public record EvaluateApprovalRuleRequestDto
{
    public string RuleType { get; init; } = "COST_ADJUSTMENT";
    public decimal Value { get; init; }
}

public record EvaluateApprovalRuleResponseDto
{
    public bool RequiresApproval { get; init; }
    public string? ApproverRole { get; init; }
}

public record RoleDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystemRole { get; init; }
    public IReadOnlyList<RolePermissionDto> Permissions { get; init; } = Array.Empty<RolePermissionDto>();
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record RolePermissionDto
{
    public int Id { get; init; }
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Scope { get; init; } = "ALL";
}

public record RolePermissionRequestDto
{
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Scope { get; init; } = "ALL";
}

public record UpsertRoleRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<RolePermissionRequestDto> Permissions { get; init; } = Array.Empty<RolePermissionRequestDto>();
}

public record AssignUserRoleRequestDto
{
    public int RoleId { get; init; }
}

public record UserRoleAssignmentDto
{
    public Guid UserId { get; init; }
    public int RoleId { get; init; }
    public DateTimeOffset AssignedAt { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
}
