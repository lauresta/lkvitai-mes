namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed class OpeningBalanceAccountTotalDto
{
    public int? StockAccountProfileId { get; set; }
    public string StockAccount { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int Rows { get; set; }
    public decimal Qty { get; set; }
    public decimal ValueEur { get; set; }
}

public sealed class OpeningBalancePreviewRowDto
{
    public int RowNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? Qty { get; set; }
    public decimal? ValueEur { get; set; }
    public string? ItemName { get; set; }
    public string? StockAccount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class OpeningBalancePreviewDto
{
    public string? FileError { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public List<OpeningBalancePreviewRowDto> Rows { get; set; } = [];
    public List<OpeningBalanceAccountTotalDto> Totals { get; set; } = [];
    public int ErrorRows { get; set; }
    public bool AlreadyLoaded { get; set; }
    public string? ExistingNumber { get; set; }
}

public sealed class OpeningBalanceConfirmDto
{
    public bool AlreadyLoaded { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Lines { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalValueEur { get; set; }
}

public sealed class OpeningBalanceDocumentDto
{
    public string Number { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public int Lines { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalValueEur { get; set; }
}
