using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

public sealed class ValuationInitialized : DomainEvent
{
    public Guid ItemId { get; set; }
    public int InventoryItemId { get; set; }
    public decimal InitialUnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? InboundShipmentId { get; set; }
    public string InitializedBy { get; set; } = string.Empty;
    public DateTime InitializedAt { get; set; }
    public Guid CommandId { get; set; }
}

public sealed class CostAdjusted : DomainEvent
{
    public Guid ItemId { get; set; }
    public int InventoryItemId { get; set; }
    public decimal OldUnitCost { get; set; }
    public decimal NewUnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AdjustedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime AdjustedAt { get; set; }
    public Guid? ApproverId { get; set; }
    public Guid CommandId { get; set; }
}

public sealed class LandedCostAllocated : DomainEvent
{
    public Guid ItemId { get; set; }
    public decimal OldUnitCost { get; set; }
    public decimal LandedCostPerUnit { get; set; }
    public decimal NewUnitCost { get; set; }
    public Guid InboundShipmentId { get; set; }
    public string AllocationMethod { get; set; } = string.Empty;
    public string AllocatedBy { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }
    public Guid CommandId { get; set; }
}

public sealed class StockWrittenDown : DomainEvent
{
    public Guid ItemId { get; set; }
    public int InventoryItemId { get; set; }
    public decimal OldUnitCost { get; set; }
    public decimal WriteDownPercentage { get; set; }
    public decimal NewUnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public decimal QuantityAffected { get; set; }
    public decimal FinancialImpact { get; set; }
    public Guid CommandId { get; set; }
}

public sealed class LandedCostApplied : DomainEvent
{
    public int ItemId { get; set; }
    public decimal FreightCost { get; set; }
    public decimal DutyCost { get; set; }
    public decimal InsuranceCost { get; set; }
    public decimal TotalLandedCost { get; set; }
    public Guid ShipmentId { get; set; }
    public string AppliedBy { get; set; } = string.Empty;
    public Guid CommandId { get; set; }
}

public sealed class WrittenDown : DomainEvent
{
    public int ItemId { get; set; }
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public Guid CommandId { get; set; }
}
