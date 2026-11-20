namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Blueprint 4.2: price of one receipt (priem) of a shipment line; a later receipt never rewrites it.</summary>
public sealed class InboundShipmentReceipt
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public int LineId { get; set; }
    public decimal Qty { get; set; }
    /// <summary>Unit price in the document currency used for this receipt.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Rate EUR per unit of currency used for this receipt (8 decimals).</summary>
    public decimal RateEurPerUnit { get; set; }
    public decimal ValueEur { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
}

/// <summary>Blueprint 4.4 dictionary of additional cost types.</summary>
public sealed class AdditionalCostType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>True: allocated to the goods; false: goes to <see cref="ExpenseAccount"/>.</summary>
    public bool IncludedInCost { get; set; } = true;
    /// <summary>Required when <see cref="IncludedInCost"/> is false, forbidden when true.</summary>
    public string? ExpenseAccount { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Blueprint 4.3 cache of Bank of Lithuania rates.</summary>
public sealed class FxRate
{
    public int Id { get; set; }
    public DateOnly RateDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    /// <summary>Units of currency per 1 EUR.</summary>
    public decimal UnitsPerEur { get; set; }
    public DateTimeOffset LoadedAt { get; set; }
}

/// <summary>Blueprint 4.4: a cost invoice of another company (carrier, broker) that refers to a purchase document.</summary>
public sealed class AdditionalCostDocument
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal? RateUnitsPerEur { get; set; }
    public DateOnly? RateDate { get; set; }
    public string? RateSource { get; set; }
    public decimal? NetEur { get; set; }
    public VatTreatment VatTreatment { get; set; }
    public string AccountingRoute { get; set; } = "Auto";
    public int BasisShipmentId { get; set; }
    public string CostTypeCode { get; set; } = string.Empty;
    public string PostingStatus { get; set; } = "NotPosted";
    public DateTimeOffset? PostedAt { get; set; }
    public string? PostedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
