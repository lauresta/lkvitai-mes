using LKvitai.MES.Modules.Warehouse.Domain.Common;

namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

public sealed class AgnumWarehouseMapping : AuditableEntity
{
    public int Id { get; set; }
    public int SndId { get; set; }
    public string AgnumName { get; set; } = string.Empty;
    public string MesVirtualWarehouseCode { get; set; } = string.Empty;
    public string ApiKeyConfigName { get; set; } = string.Empty;
    public bool IsImportEnabled { get; set; }
}

public sealed class AgnumProductLink : AuditableEntity
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int SndId { get; set; }
    public int AgnumProductId { get; set; }
    public string AgnumCode { get; set; } = string.Empty;
    public bool AgnumEnabled { get; set; }
    public DateTime? AgnumModifiedAt { get; set; }
    public DateTime? LastImportedAt { get; set; }
    public string? RawHash { get; set; }

    public Item? Item { get; set; }
}

public sealed class ItemExternalAttribute
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string SourceSystem { get; set; } = "AGNUM";
    public string SourceContext { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }

    public Item? Item { get; set; }
}

public sealed class AgnumBalanceImportRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SndId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public int ProductCount { get; set; }
    public int BalanceCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorSummary { get; set; }
}

public sealed class AgnumVirtualWarehouseBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportRunId { get; set; }
    public int SndId { get; set; }
    public int AgnumProductId { get; set; }
    public int? ItemId { get; set; }
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string? SourceHash { get; set; }

    public AgnumBalanceImportRun? ImportRun { get; set; }
}
