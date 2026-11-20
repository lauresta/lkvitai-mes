namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Blueprint 4.5. Stock account profile referenced by item categories.</summary>
public sealed class StockAccountProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Agnum stock account, e.g. "20110".</summary>
    public string StockAccount { get; set; } = string.Empty;
    /// <summary>Agnum warehouse (ID_SND).</summary>
    public int AgnumSndId { get; set; }
    public string? PriceDifferenceAccount { get; set; }
    public string? WriteOffAccount { get; set; }
    public string? MarkdownAccount { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// VAT treatment of a purchase. Minimal W3 definition; the purchase document stage (W2) owns the
/// full model and is expected to reuse these names (stored as strings).
/// </summary>
public enum VatTreatment
{
    Domestic = 0,
    EuAcquisition = 1,
    Import = 2
}

/// <summary>Blueprint 4.6 "agnum_posting_rules": (profile x VatTreatment) -> Agnum scheme + VAT code.</summary>
public sealed class AgnumPostingRule
{
    public int Id { get; set; }
    public int StockAccountProfileId { get; set; }
    public VatTreatment VatTreatment { get; set; }
    /// <summary>Agnum ID_SCH.</summary>
    public int SchemeId { get; set; }
    /// <summary>Agnum PVM_KOD; empty for EuAcquisition and Import.</summary>
    public string? VatCode { get; set; }
}

/// <summary>Insert-only journal of accounting configuration changes (who, when, before, after).</summary>
public sealed class AccountingConfigAuditEntry
{
    public long Id { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

/// <summary>Blueprint 4.11. Set at sync from VAT code prefix and country; manually editable.</summary>
public enum SupplierKind
{
    Domestic = 0,
    Eu = 1,
    NonEu = 2
}
