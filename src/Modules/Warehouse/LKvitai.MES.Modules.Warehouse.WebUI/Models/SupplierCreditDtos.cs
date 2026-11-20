namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed class SupplierCreditPrefillDto
{
    public int OriginalShipmentId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal? NetAmount { get; set; }
    public decimal CreditedNet { get; set; }
    public List<SupplierCreditPrefillLineDto> Lines { get; set; } = new();
}

public sealed class SupplierCreditPrefillLineDto
{
    public int OriginalLineId { get; set; }
    public int ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public bool IsService { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal InvoiceQty { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class SupplierCreditLineRequestDto
{
    public int OriginalLineId { get; set; }
    public decimal ReturnQty { get; set; }
    public string? FromLocation { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed class SupplierCreditRequestDto
{
    public int OriginalShipmentId { get; set; }
    public string DocumentKind { get; set; } = "CreditNote";
    public string CreditNumber { get; set; } = string.Empty;
    public DateOnly CreditDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public string? Notes { get; set; }
    public bool Post { get; set; } = true;
    public List<SupplierCreditLineRequestDto> Lines { get; set; } = new();
}

public sealed class SupplierCreditLineDto
{
    public int Id { get; set; }
    public int OriginalLineId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ReturnQty { get; set; }
    public string? FromLocation { get; set; }
    public decimal NetAmount { get; set; }
    public decimal? NetEur { get; set; }
}

public sealed class SupplierCreditDto
{
    public int Id { get; set; }
    public string DocumentKind { get; set; } = string.Empty;
    public int OriginalShipmentId { get; set; }
    public string CreditNumber { get; set; } = string.Empty;
    public DateOnly CreditDate { get; set; }
    public string? OriginalInvoiceNumber { get; set; }
    public DateOnly? OriginalInvoiceDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal? AgnumNetEur { get; set; }
    public decimal? AgnumVatEur { get; set; }
    public decimal? AgnumGrossEur { get; set; }
    public string? Notes { get; set; }
    public string PostingStatus { get; set; } = "NotPosted";
    public DateTimeOffset? PostedAt { get; set; }
    public string? PostedBy { get; set; }
    public List<SupplierCreditLineDto> Lines { get; set; } = new();
    public List<PostingProblemDto> Problems { get; set; } = new();
}
