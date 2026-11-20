using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

/// <summary>Blueprint 4.7 "Сверка": compares what Warehouse sent with what the service reports for the period.</summary>
public sealed class AgnumOutboxReconciler
{
    public const int DefaultDays = 90;

    private readonly WarehouseDbContext _db;
    private readonly IAgnumPostingClient _client;
    private readonly TimeProvider _time;

    public AgnumOutboxReconciler(WarehouseDbContext db, IAgnumPostingClient client, TimeProvider? time = null)
    {
        _db = db;
        _client = client;
        _time = time ?? TimeProvider.System;
    }

    public async Task<int> RunAsync(int days, CancellationToken ct)
    {
        var settings = await _db.AgnumSendingSettings.FirstOrDefaultAsync(ct);
        if (settings is not { Enabled: true }) return 0;

        var now = _time.GetUtcNow();
        var to = DateOnly.FromDateTime(StockBusinessDate.From(now.UtcDateTime));
        var from = to.AddDays(-(Math.Clamp(days, 1, 365) - 1));
        if (settings.StartDate is { } start && from < start) from = start;

        try
        {
            var period = await _client.GetPeriodAsync(from, to, ct);
            var findings = await CompareAsync(period, from, to, now, ct);
            _db.AgnumReconciliationFindings.RemoveRange(await _db.AgnumReconciliationFindings.ToListAsync(ct));
            _db.AgnumReconciliationFindings.AddRange(findings);
            settings.LastReconciledAt = now;
            settings.LastReconciliationFindings = findings.Count;
            settings.LastReconciliationError = null;
            await _db.SaveChangesAsync(ct);
            return findings.Count;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AgnumPostingHttpException or JsonException)
        {
            settings.LastReconciliationError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            await _db.SaveChangesAsync(ct);
            return -1;
        }
    }

    public async Task<List<AgnumReconciliationFinding>> CompareAsync(AgnumPeriod period, DateOnly from, DateOnly to, DateTimeOffset now, CancellationToken ct)
    {
        var findings = new List<AgnumReconciliationFinding>();
        AgnumReconciliationFinding F(string kind, string details, string? type = null, int? id = null, Guid? key = null, string? number = null)
            => new() { RunAt = now, Kind = kind, Details = details, DocumentType = type, DocumentId = id, Key = key, DocumentNumber = number };

        var all = await _db.AgnumOutbox.AsNoTracking().ToListAsync(ct);
        var rows = all.Where(r => r.Operation == OutboxOperation.Post).ToList();
        var open = all.Where(r => r.Status != OutboxStatus.Annulled && !(r.Operation == OutboxOperation.Annul && r.Status == OutboxStatus.Sent)).Select(r => r.Key).ToHashSet();
        var byKey = period.Postings.Where(p => Guid.TryParse(p.Key, out _)).GroupBy(p => Guid.Parse(p.Key!)).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var row in rows.Where(r => r.Status == OutboxStatus.Sent && r.DocumentDate >= from && r.DocumentDate <= to))
        {
            var found = byKey.GetValueOrDefault(row.Key)?.FirstOrDefault(d => d.Revision == row.Revision);
            if (found is null)
            {
                findings.Add(F(ReconciliationKinds.MissingInAgnum, "Sent, but the service does not report it for the period.",
                    row.DocumentType, row.DocumentId, row.Key, row.DocumentNumber));
                continue;
            }

            var diff = Differences(row, found);
            if (diff.Count > 0)
                findings.Add(F(ReconciliationKinds.Mismatch, string.Join("; ", diff), row.DocumentType, row.DocumentId, row.Key, row.DocumentNumber));
        }

        foreach (var (key, docs) in byKey.Where(k => !open.Contains(k.Key)))
        {
            var d = docs[0];
            findings.Add(F(ReconciliationKinds.UnexpectedInAgnum,
                $"Agnum holds a Warehouse document (id_dok {d.IdDok}) that Warehouse has not sent or has annulled.", null, null, key, d.DocNo));
        }

        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.AgnumClientId != null).ToDictionaryAsync(s => s.Id, s => s.AgnumClientId!.Value, ct);
        var manual = new List<(string Type, int Id, int SupplierId, string Number, DateOnly Date)>();
        manual.AddRange((await _db.InboundShipments.AsNoTracking()
            .Where(s => s.PostingStatus == "Posted" && s.AccountingRoute == "ManualInAgnum" && s.InvoiceDate != null && s.InvoiceDate >= from && s.InvoiceDate <= to)
            .Select(s => new { s.Id, s.SupplierId, s.InvoiceNumber, s.InvoiceDate }).ToListAsync(ct))
            .Select(s => (OutboxDocumentType.Shipment, s.Id, s.SupplierId, s.InvoiceNumber ?? string.Empty, s.InvoiceDate!.Value)));
        manual.AddRange((await _db.AdditionalCostDocuments.AsNoTracking()
            .Where(d => d.PostingStatus == "Posted" && d.AccountingRoute == "ManualInAgnum" && d.DocumentDate >= from && d.DocumentDate <= to)
            .Select(d => new { d.Id, d.SupplierId, d.DocumentNumber, d.DocumentDate }).ToListAsync(ct))
            .Select(d => (OutboxDocumentType.CostDocument, d.Id, d.SupplierId, d.DocumentNumber, d.DocumentDate)));

        var foreign = period.ForeignDocuments.ToList();
        var matched = new HashSet<AgnumPostedDocument>();
        foreach (var m in manual)
        {
            if (!suppliers.TryGetValue(m.SupplierId, out var clientId)) continue;
            var norm = PurchaseMath.NormalizeInvoiceNumber(m.Number);
            var hit = foreign.Concat(period.Postings).FirstOrDefault(f => f.ClientId == clientId && PurchaseMath.NormalizeInvoiceNumber(f.DocNo) == norm);
            if (hit is null)
                findings.Add(F(ReconciliationKinds.ManualNotFound, "Marked as entered manually, but not found in Agnum by supplier and number.", m.Type, m.Id, null, m.Number));
            else matched.Add(hit);
        }

        foreach (var f in foreign.Where(f => !matched.Contains(f)))
            findings.Add(F(ReconciliationKinds.ForeignDocument,
                $"Purchase on a stock account created in Agnum outside Warehouse (id_dok {f.IdDok}, supplier {f.ClientId}, gross {f.Gross}).", null, null, null, f.DocNo));
        return findings;
    }

    private static List<string> Differences(AgnumDocumentOutbox row, AgnumPostedDocument found)
    {
        var diff = new List<string>();
        var req = JsonSerializer.Deserialize<AgnumPurchasePostingRequest>(row.PayloadJson);
        if (!string.Equals(found.Hash, row.PayloadHash, StringComparison.OrdinalIgnoreCase)) diff.Add("content hash differs");
        if (req is null) return diff;
        if (found.Net != req.Net) diff.Add($"net {found.Net} instead of {req.Net}");
        if (found.Vat != req.Vat) diff.Add($"vat {found.Vat} instead of {req.Vat}");
        if (found.Gross != req.Gross) diff.Add($"gross {found.Gross} instead of {req.Gross}");
        foreach (var p in req.Postings)
        {
            if (found.Postings?.Any(x => x.Debit == p.DebitAccount && x.Amount == p.Amount && x.AmountEur == p.AmountEur) != true)
                diff.Add($"posting D{p.DebitAccount} {p.Amount}/{p.AmountEur} EUR is missing");
        }

        return diff;
    }
}
