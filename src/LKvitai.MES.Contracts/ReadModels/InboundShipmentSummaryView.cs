namespace LKvitai.MES.Contracts.ReadModels;

public sealed class InboundShipmentSummaryView
{
    public string Id { get; set; } = string.Empty;
    public Guid ShipmentId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalExpectedQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
    public decimal CompletionPercent { get; set; }
    public int TotalLines { get; set; }
    public string Status { get; set; } = "Draft";
    public DateOnly? ExpectedDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public static string ComputeId(Guid shipmentId)
        => shipmentId.ToString("N");
}
