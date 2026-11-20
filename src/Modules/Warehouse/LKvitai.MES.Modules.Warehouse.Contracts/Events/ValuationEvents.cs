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

/// <summary>Kinds of <see cref="StockValueChanged"/> (blueprint 4.1).</summary>
public static class StockValueKinds
{
    public const string Opening = "Opening";
    public const string Receipt = "Receipt";
    public const string ReceiptTrueUp = "ReceiptTrueUp";
    public const string Issue = "Issue";
    public const string ReturnIn = "ReturnIn";
    public const string AdjustmentIn = "AdjustmentIn";
    public const string AdjustmentOut = "AdjustmentOut";
    public const string Scrap = "Scrap";
    public const string SupplierReturn = "SupplierReturn";
    public const string PriceDifference = "PriceDifference";
    public const string Revaluation = "Revaluation";

    public static readonly IReadOnlyList<string> All =
    [
        Opening, Receipt, ReceiptTrueUp, Issue, ReturnIn, AdjustmentIn, AdjustmentOut,
        Scrap, SupplierReturn, PriceDifference, Revaluation
    ];
}

/// <summary>
/// The only event that changes the value (EUR) of an item's stock. Written in the same transaction
/// as the quantity event that caused it.
/// </summary>
public sealed class StockValueChanged : DomainEvent
{
    public int ItemId { get; set; }
    public string Kind { get; set; } = string.Empty;
    /// <summary>Change of valued quantity; may be 0.</summary>
    public decimal QtyDelta { get; set; }
    /// <summary>Change of value in EUR, 2 decimals.</summary>
    public decimal ValueDeltaEur { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    /// <summary>Stock account profile snapshot (blueprint 4.5, filled from W3).</summary>
    public int? StockAccountProfileId { get; set; }
    /// <summary>Date (time 00:00, Kind unspecified) in Europe/Vilnius.</summary>
    public DateTime BusinessDate { get; set; }
    public string? Reason { get; set; }
}
