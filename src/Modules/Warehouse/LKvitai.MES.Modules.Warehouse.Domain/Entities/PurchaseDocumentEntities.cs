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
    public int PostingRevision { get; set; }
    public Guid? AgnumKey { get; set; }
    public string? ManualReason { get; set; }
    public string? PostingSnapshotJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Blueprint 4.8: a supplier credit note (price-only) or a supplier return (goods back plus credit), linked to the
/// original purchase document. Amounts are entered positive; the signed Agnum* amounts (negative) are what the
/// sender (W4) puts into Agnum, with <see cref="OriginalInvoiceNumber"/> / <see cref="OriginalInvoiceDate"/> as the reference.
/// </summary>
public sealed class SupplierCreditDocument
{
    public const string KindCreditNote = "CreditNote";
    public const string KindSupplierReturn = "SupplierReturn";

    public int Id { get; set; }
    /// <summary>CreditNote or SupplierReturn.</summary>
    public string DocumentKind { get; set; } = KindCreditNote;
    public int OriginalShipmentId { get; set; }
    public int SupplierId { get; set; }
    public string CreditNumber { get; set; } = string.Empty;
    public DateOnly CreditDate { get; set; }
    /// <summary>Reference to the original document (copied at creation).</summary>
    public string? OriginalInvoiceNumber { get; set; }
    public DateOnly? OriginalInvoiceDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal? RateUnitsPerEur { get; set; }
    public DateOnly? RateDate { get; set; }
    public string? RateSource { get; set; }
    public decimal? NetEur { get; set; }
    public decimal? VatEur { get; set; }
    public decimal? GrossEur { get; set; }
    /// <summary>Negative EUR amounts for Agnum, set at posting.</summary>
    public decimal? AgnumNetEur { get; set; }
    public decimal? AgnumVatEur { get; set; }
    public decimal? AgnumGrossEur { get; set; }
    public VatTreatment? VatTreatment { get; set; }
    public string AccountingRoute { get; set; } = "Auto";
    public string? Notes { get; set; }
    public string PostingStatus { get; set; } = "NotPosted";
    public DateTimeOffset? PostedAt { get; set; }
    public string? PostedBy { get; set; }
    public int PostingRevision { get; set; }
    /// <summary>Idempotency key of the Agnum posting (blueprint 4.7); set when the credit is queued.</summary>
    public Guid? AgnumKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<SupplierCreditLine> Lines { get; set; } = new List<SupplierCreditLine>();
}

public sealed class SupplierCreditLine
{
    public int Id { get; set; }
    public int CreditId { get; set; }
    public int OriginalLineId { get; set; }
    public int ItemId { get; set; }
    /// <summary>Quantity going back to the supplier; 0 for a price-only credit.</summary>
    public decimal ReturnQty { get; set; }
    /// <summary>Location the returned goods leave from (required when <see cref="ReturnQty"/> is above 0).</summary>
    public string? FromLocation { get; set; }
    /// <summary>Credit amount of the line in the document currency (positive).</summary>
    public decimal NetAmount { get; set; }
    public decimal? NetEur { get; set; }

    public SupplierCreditDocument? Credit { get; set; }
    public InboundShipmentLine? OriginalLine { get; set; }
    public Item? Item { get; set; }
}
