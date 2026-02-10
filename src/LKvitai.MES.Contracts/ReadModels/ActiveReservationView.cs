namespace LKvitai.MES.Contracts.ReadModels;

public sealed class ActiveReservationView
{
    public string Id { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal ReservedQty { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public static string ComputeId(Guid reservationId)
        => reservationId.ToString("N");
}
