namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record CycleCountLineDto
{
    public int LocationId { get; init; }
    public int ItemId { get; init; }
    public decimal SystemQty { get; init; }
    public decimal PhysicalQty { get; init; }
    public decimal Delta { get; init; }
    public DateTimeOffset? CountedAt { get; init; }
    public string? CountedBy { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? AdjustmentApprovedBy { get; init; }
    public DateTimeOffset? AdjustmentApprovedAt { get; init; }
}

public record CycleCountDto
{
    public Guid Id { get; init; }
    public string CountNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset ScheduledDate { get; init; }
    public string AbcClass { get; init; } = "ALL";
    public string AssignedOperator { get; init; } = string.Empty;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CountedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public IReadOnlyList<int> LocationIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<CycleCountLineDto> Lines { get; init; } = Array.Empty<CycleCountLineDto>();
}

public record ScheduleCycleCountRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset ScheduledDate { get; init; }
    public string AbcClass { get; init; } = "ALL";
    public string AssignedOperator { get; init; } = string.Empty;
    public IReadOnlyList<int>? LocationIds { get; init; }
}

public record RecordCountRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public decimal PhysicalQty { get; init; }
    public string? LocationCode { get; init; }
    public string? ItemBarcode { get; init; }
    public int? LocationId { get; init; }
    public int? ItemId { get; init; }
    public string? Reason { get; init; }
    public string? CountedBy { get; init; }
}

public record RecordCountResponseDto
{
    public CycleCountDto CycleCount { get; init; } = new();
    public bool HasDiscrepancy { get; init; }
    public string? Warning { get; init; }
}

public record DiscrepancyLineDto
{
    public Guid LineId { get; init; }
    public int LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public decimal SystemQty { get; init; }
    public decimal PhysicalQty { get; init; }
    public decimal Variance { get; init; }
    public decimal VariancePercent { get; init; }
    public decimal ValueImpact { get; init; }
    public string? AdjustmentApprovedBy { get; init; }
    public DateTimeOffset? AdjustmentApprovedAt { get; init; }
}

public record CycleCountLineDetailDto
{
    public Guid Id { get; init; }
    public int LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemBarcode { get; init; } = string.Empty;
    public decimal SystemQty { get; init; }
    public decimal PhysicalQty { get; init; }
    public decimal Delta { get; init; }
    public DateTimeOffset? CountedAt { get; init; }
    public string? CountedBy { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public record ApproveAdjustmentRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public IReadOnlyList<Guid> LineIds { get; init; } = Array.Empty<Guid>();
    public string? ApprovedBy { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record ApproveAdjustmentResponseDto
{
    public int ApprovedLineCount { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; init; }
}
