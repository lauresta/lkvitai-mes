using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

public sealed record SnapshotLine(int LineId, decimal ReceivedQty, decimal? PreFinalEur, decimal PreBookedEur, decimal? PreInvoiceQty, decimal? PreInvoiceUnitPrice);

public sealed record SnapshotCost(int CostId, string Currency, decimal? AmountEur);

/// <summary>A value request the posting wrote: reversing it with the opposite difference restores the stock value.</summary>
public sealed record SnapshotValue(string Sku, decimal Qty, decimal DiffEur);

/// <summary>
/// What a posting overwrote on the document (blueprint 4.8 un-post: back to NotPosted and to the value before posting).
/// Pure functions on the entities; the caller persists them together with the reversing stock value events.
/// </summary>
public sealed class PostingSnapshot
{
    private static readonly JsonSerializerOptions Options = new();

    public decimal? RateUnitsPerEur { get; set; }
    public DateOnly? RateDate { get; set; }
    public string? RateSource { get; set; }
    public string? RateReason { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? NetEur { get; set; }
    public decimal? VatEur { get; set; }
    public decimal? GrossEur { get; set; }
    public string? VatTreatment { get; set; }
    public List<SnapshotCost> Costs { get; set; } = new();
    public List<SnapshotLine> Lines { get; set; } = new();
    public List<SnapshotValue> Values { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static PostingSnapshot? FromJson(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PostingSnapshot>(json, Options);

    /// <summary>Capture before the posting changes the shipment.</summary>
    public static PostingSnapshot Capture(InboundShipment s) => new()
    {
        RateUnitsPerEur = s.RateUnitsPerEur, RateDate = s.RateDate, RateSource = s.RateSource, RateReason = s.RateReason,
        GrossAmount = s.GrossAmount, NetEur = s.NetEur, VatEur = s.VatEur, GrossEur = s.GrossEur,
        VatTreatment = s.VatTreatment?.ToString(),
        Costs = s.AdditionalCosts.Select(c => new SnapshotCost(c.Id, c.Currency, c.AmountEur)).ToList(),
        Lines = s.Lines.Select(l => new SnapshotLine(l.Id, l.ReceivedQty, l.FinalValueEur, l.BookedValueEur, l.InvoiceQty, l.InvoiceUnitPrice)).ToList()
    };

    /// <summary>Capture before a cost document changes the lines of its basis shipment.</summary>
    public static PostingSnapshot Capture(AdditionalCostDocument d, IEnumerable<InboundShipmentLine> basisLines) => new()
    {
        RateUnitsPerEur = d.RateUnitsPerEur, RateDate = d.RateDate, RateSource = d.RateSource, GrossAmount = d.GrossAmount, NetEur = d.NetEur,
        Lines = basisLines.Select(l => new SnapshotLine(l.Id, l.ReceivedQty, l.FinalValueEur, l.BookedValueEur, l.InvoiceQty, l.InvoiceUnitPrice)).ToList()
    };

    /// <summary>Blocks the reversal when quantities moved after posting: later receipts were valued at the final price.</summary>
    public IReadOnlyList<string> ReceiptChangesSincePosting(IEnumerable<InboundShipmentLine> lines)
    {
        var current = lines.ToDictionary(l => l.Id);
        return Lines.Where(s => !current.TryGetValue(s.LineId, out var l) || l.ReceivedQty != s.ReceivedQty)
            .Select(s => $"line {s.LineId}").ToList();
    }

    public void Restore(InboundShipment s)
    {
        s.RateUnitsPerEur = RateUnitsPerEur; s.RateDate = RateDate; s.RateSource = RateSource; s.RateReason = RateReason;
        s.GrossAmount = GrossAmount; s.NetEur = NetEur; s.VatEur = VatEur; s.GrossEur = GrossEur;
        s.VatTreatment = Enum.TryParse<VatTreatment>(VatTreatment, out var t) ? t : null;
        foreach (var c in s.AdditionalCosts)
        {
            var saved = Costs.FirstOrDefault(x => x.CostId == c.Id);
            if (saved is not null) { c.Currency = saved.Currency; c.AmountEur = saved.AmountEur; }
        }

        RestoreLines(s.Lines);
        s.PostingStatus = "NotPosted";
        s.PostedAt = null;
        s.PostedBy = null;
        s.PostingRevision++;
        s.PostingSnapshotJson = null;
    }

    public void Restore(AdditionalCostDocument d, IEnumerable<InboundShipmentLine> basisLines)
    {
        d.RateUnitsPerEur = RateUnitsPerEur; d.RateDate = RateDate; d.RateSource = RateSource;
        d.GrossAmount = GrossAmount ?? d.NetAmount + d.VatAmount; d.NetEur = NetEur;
        RestoreLines(basisLines);
        d.PostingStatus = "NotPosted";
        d.PostedAt = null;
        d.PostedBy = null;
        d.PostingRevision++;
        d.PostingSnapshotJson = null;
    }

    private void RestoreLines(IEnumerable<InboundShipmentLine> lines)
    {
        foreach (var l in lines)
        {
            var saved = Lines.FirstOrDefault(x => x.LineId == l.Id);
            if (saved is null) continue;
            l.FinalValueEur = saved.PreFinalEur;
            l.BookedValueEur = saved.PreBookedEur;
            l.InvoiceQty = saved.PreInvoiceQty;
            l.InvoiceUnitPrice = saved.PreInvoiceUnitPrice;
        }
    }
}
