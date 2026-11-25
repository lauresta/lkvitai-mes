using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>Quantity of one item still expected on one open inbound shipment (item card "Laukiama").</summary>
public sealed record ItemIncomingRowDto(
    int ShipmentId,
    string ReferenceNumber,
    string? SupplierName,
    DateOnly? ExpectedDate,
    string Status,
    decimal ExpectedQty,
    decimal ReceivedQty,
    decimal OpenQty);

/// <summary>Builds the open inbound quantity of an item: lines of shipments that are not yet fully received.</summary>
public static class ItemIncomingRows
{
    /// <summary>Shipment statuses whose lines can still be received.</summary>
    public static readonly string[] OpenStatuses = ["Draft", "Partial"];

    private const string CreditNoteKind = "CreditNote";

    public static IReadOnlyList<ItemIncomingRowDto> Build(int itemId, IEnumerable<InboundShipment> shipments)
    {
        return shipments
            .Where(IsOpenDelivery)
            .Select(shipment => ToRow(itemId, shipment))
            .Where(row => row.OpenQty > 0m)
            .OrderBy(row => row.ExpectedDate ?? DateOnly.MaxValue)
            .ThenBy(row => row.ShipmentId)
            .ToList();
    }

    private static bool IsOpenDelivery(InboundShipment shipment)
        => OpenStatuses.Contains(shipment.Status, StringComparer.OrdinalIgnoreCase)
           && !string.Equals(shipment.DocumentKind, CreditNoteKind, StringComparison.OrdinalIgnoreCase);

    private static ItemIncomingRowDto ToRow(int itemId, InboundShipment shipment)
    {
        var lines = shipment.Lines.Where(line => line.ItemId == itemId).ToList();
        var expected = lines.Sum(line => line.ExpectedQty);
        var received = lines.Sum(line => line.ReceivedQty);
        var open = lines.Sum(line => Math.Max(0m, line.ExpectedQty - line.ReceivedQty));

        return new ItemIncomingRowDto(
            shipment.Id,
            shipment.ReferenceNumber,
            shipment.Supplier?.Name,
            shipment.ExpectedDate,
            shipment.Status,
            expected,
            received,
            open);
    }
}
