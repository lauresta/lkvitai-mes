namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>One line of an active reservation, flattened from the reservation stream and its summary view.</summary>
public sealed record ReservationLineSource(
    Guid ReservationId,
    string Purpose,
    string Status,
    DateTime CreatedAt,
    string Sku,
    string? Location,
    decimal Qty);

/// <summary>The sales order that owns a reservation, when there is one.</summary>
public sealed record ReservationOrderRef(
    Guid SalesOrderId,
    string OrderNumber,
    string? CustomerName,
    string Status);

/// <summary>Reserved quantity of one item at one location for one reservation (item card "Rezervacijos").</summary>
public sealed record ItemReservationRowDto(
    Guid ReservationId,
    Guid? SalesOrderId,
    string? OrderNumber,
    string? CustomerName,
    string? Location,
    decimal Qty,
    string Status,
    DateTime CreatedAt,
    string Purpose);

/// <summary>Builds the item card reservation rows: lines of one SKU, one row per reservation and location.</summary>
public static class ItemReservationRows
{
    public static IReadOnlyList<ItemReservationRowDto> Build(
        string sku,
        IEnumerable<ReservationLineSource> lines,
        IReadOnlyDictionary<Guid, ReservationOrderRef> ordersByReservation)
    {
        return lines
            .Where(line => line.Qty > 0m && string.Equals(line.Sku, sku, StringComparison.OrdinalIgnoreCase))
            .GroupBy(line => (line.ReservationId, Location: NormalizeLocation(line.Location)))
            .Select(group => ToRow(group.First(), group.Key.Location, group.Sum(x => x.Qty), ordersByReservation))
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.OrderNumber ?? row.Purpose, StringComparer.Ordinal)
            .ThenBy(row => row.Location, StringComparer.Ordinal)
            .ToList();
    }

    private static ItemReservationRowDto ToRow(
        ReservationLineSource line,
        string? location,
        decimal qty,
        IReadOnlyDictionary<Guid, ReservationOrderRef> ordersByReservation)
    {
        ordersByReservation.TryGetValue(line.ReservationId, out var order);
        return new ItemReservationRowDto(
            line.ReservationId,
            order?.SalesOrderId,
            order?.OrderNumber,
            order?.CustomerName,
            location,
            qty,
            order?.Status ?? line.Status,
            line.CreatedAt,
            line.Purpose);
    }

    private static string? NormalizeLocation(string? location)
        => string.IsNullOrWhiteSpace(location) ? null : location.Trim();
}
