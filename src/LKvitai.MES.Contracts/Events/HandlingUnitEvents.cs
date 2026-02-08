using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

/// <summary>
/// Event published when a new handling unit is created.
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class HandlingUnitCreatedEvent : DomainEvent
{
    public Guid HuId { get; set; }
    public string LPN { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // PALLET, BOX, BAG, UNIT
    public string WarehouseId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
}

/// <summary>
/// Event published when a line is added directly to a handling unit.
/// Used for Split/Merge and adjustment scenarios where no StockMoved is involved.
/// For receipt scenarios, lines are derived from StockMoved events (design spec).
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class LineAddedToHandlingUnitEvent : DomainEvent
{
    public Guid HuId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

/// <summary>
/// Event published when a line is removed directly from a handling unit.
/// Used for Split/Merge scenarios where no StockMoved is involved.
/// For pick scenarios, line removals are derived from StockMoved events (design spec).
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class LineRemovedFromHandlingUnitEvent : DomainEvent
{
    public Guid HuId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

/// <summary>
/// Event published when a handling unit is sealed.
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class HandlingUnitSealedEvent : DomainEvent
{
    public Guid HuId { get; set; }
    public DateTime SealedAt { get; set; }
}

// ──────────────────────────────────────────────────────────────────────
// Split / Merge events — Phase 1 event definitions only.
// TODO: Implement Split/Merge command handlers and projection Apply
//       methods when Task 19 (Split and Merge Operations) is reached.
//       Do NOT implement half — these are schema-only for now.
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Event published when a handling unit is split.
/// TODO: Implement Apply method in HandlingUnitProjection when Task 19 is reached.
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class HandlingUnitSplitEvent : DomainEvent
{
    public Guid SourceHuId { get; set; }
    public Guid NewHuId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

/// <summary>
/// Event published when handling units are merged.
/// TODO: Implement Apply method in HandlingUnitProjection when Task 19 is reached.
/// Self-contained event data (V-5 Rule B compliance).
/// </summary>
public class HandlingUnitMergedEvent : DomainEvent
{
    public List<Guid> SourceHuIds { get; set; } = new();
    public Guid TargetHuId { get; set; }
}
