namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

/// <summary>Reserved quantity of one item at one location for one active reservation.</summary>
public record ItemReservationRowDto
{
    public Guid ReservationId { get; init; }
    public Guid? SalesOrderId { get; init; }
    public string? OrderNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? Location { get; init; }
    public decimal Qty { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Purpose { get; init; } = string.Empty;
}

/// <summary>Quantity of one item still expected on one open inbound shipment.</summary>
public record ItemIncomingRowDto
{
    public int ShipmentId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string? SupplierName { get; init; }
    public DateOnly? ExpectedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal ExpectedQty { get; init; }
    public decimal ReceivedQty { get; init; }
    public decimal OpenQty { get; init; }
}
