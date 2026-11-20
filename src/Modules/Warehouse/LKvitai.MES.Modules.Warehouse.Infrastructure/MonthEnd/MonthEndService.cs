using System.Text.Json;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.MonthEnd;

/// <summary>Source of the value ledger; the production one reads the Marten event store (never a projection).</summary>
public interface IStockValueEventSource
{
    Task<IReadOnlyList<StockValueChanged>> LoadAsync(DateOnly upTo, CancellationToken ct);
}

public sealed class MartenStockValueEventSource : IStockValueEventSource
{
    private readonly IDocumentStore _store;
    public MartenStockValueEventSource(IDocumentStore store) => _store = store;

    public async Task<IReadOnlyList<StockValueChanged>> LoadAsync(DateOnly upTo, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        var raw = await Marten.QueryableExtensions.ToListAsync(session.Events.QueryAllRawEvents().OrderBy(e => e.Sequence), ct);
        var limit = upTo.ToDateTime(TimeOnly.MinValue).AddDays(1);
        return raw.Select(e => e.Data).OfType<StockValueChanged>().Where(e => e.ItemId > 0 && e.BusinessDate < limit).ToList();
    }
}

public sealed class MonthEndException : Exception
{
    public IReadOnlyList<string> Blockers { get; }
    public MonthEndException(string message, IReadOnlyList<string>? blockers = null) : base(message) => Blockers = blockers ?? [];
}

public sealed record PeriodStateDto(
    int Year, int Month, string Status, DateTimeOffset? ClosedAt, string? ClosedBy,
    DateTimeOffset? ReopenedAt, string? ReopenedBy, string? ReopenReason, IReadOnlyList<string> Blockers);

/// <summary>The "month is closed" rule used by every place that creates a document or value event dated in a month.</summary>
public static class AccountingPeriods
{
    public const string ProblemCode = "PeriodClosed";

    public static async Task<bool> IsClosedAsync(WarehouseDbContext db, DateOnly date, CancellationToken ct)
        => await db.AccountingPeriods.AsNoTracking().AnyAsync(p => p.Year == date.Year && p.Month == date.Month && p.Status == PeriodStatus.Closed, ct);

    /// <summary>A posting problem when any of the dates falls into a closed month; null when all are open.</summary>
    public static async Task<PostingProblem?> CheckAsync(WarehouseDbContext db, CancellationToken ct, params DateOnly?[] dates)
    {
        foreach (var d in dates.Where(d => d is not null).Select(d => d!.Value).Distinct())
        {
            if (await IsClosedAsync(db, d, ct))
                return new PostingProblem(ProblemCode, $"The month {d:yyyy-MM} is closed; no document dated in it can be posted or un-posted.");
        }

        return null;
    }
}

/// <summary>Blueprint 4.9 month end: report, blockers, close with snapshot, audited reopen.</summary>
public sealed class MonthEndService
{
    private static readonly JsonSerializerOptions Json = new();
    private readonly WarehouseDbContext _db;
    private readonly IStockValueEventSource _events;
    private readonly TimeProvider _time;

    public MonthEndService(WarehouseDbContext db, IStockValueEventSource events, TimeProvider? time = null)
    {
        _db = db;
        _events = events;
        _time = time ?? TimeProvider.System;
    }

    private DateOnly Today => DateOnly.FromDateTime(StockBusinessDate.From(_time.GetUtcNow().UtcDateTime));

    public async Task<MonthEndReport> GetReportAsync(int year, int month, CancellationToken ct)
    {
        var snapshot = await _db.MonthEndSnapshots.AsNoTracking().FirstOrDefaultAsync(s => s.Year == year && s.Month == month, ct);
        if (snapshot is not null && await IsClosedAsync(year, month, ct))
            return JsonSerializer.Deserialize<MonthEndReport>(snapshot.ReportJson, Json)!;
        return await BuildLiveAsync(year, month, ct);
    }

    public async Task<MonthEndReport> BuildLiveAsync(int year, int month, CancellationToken ct)
    {
        var end = new DateOnly(year, month, 1).AddMonths(1).AddDays(-1);
        var loaded = await _events.LoadAsync(end, ct);
        var rows = loaded.Select(e => new ValueEventRow(e.ItemId, e.Kind, e.QtyDelta, e.ValueDeltaEur, e.OffLedgerEur,
            DateOnly.FromDateTime(e.BusinessDate), e.StockAccountProfileId, e.DocumentType, e.DocumentId, e.DocumentNumber, e.Reason)).ToList();
        var profiles = (await _db.StockAccountProfiles.AsNoTracking().ToListAsync(ct))
            .Select(p => new StockProfileRef(p.Id, p.StockAccount, p.Name, p.IsDefault)).ToList();
        var itemIds = rows.Select(r => r.ItemId).Distinct().ToList();
        var skus = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InternalSKU, ct);
        var facts = await LoadFactsAsync(profiles, ct);
        return MonthEndStatement.Build(year, month, rows, profiles, facts, skus);
    }

    private async Task<List<AgnumDocFact>> LoadFactsAsync(IReadOnlyList<StockProfileRef> profiles, CancellationToken ct)
    {
        var stockAccounts = profiles.Select(p => p.StockAccount).ToHashSet(StringComparer.Ordinal);
        var all = await _db.AgnumOutbox.AsNoTracking().ToListAsync(ct);
        var annulled = all.Where(r => r.Operation == OutboxOperation.Annul && r.Status == OutboxStatus.Sent).Select(r => (r.Key, r.Revision)).ToHashSet();
        var facts = new List<AgnumDocFact>();
        foreach (var row in all.Where(r => r.Operation == OutboxOperation.Post && r.Status != OutboxStatus.Annulled && !annulled.Contains((r.Key, r.Revision))))
        {
            var request = JsonSerializer.Deserialize<AgnumPurchasePostingRequest>(row.PayloadJson, Json);
            if (request is null) continue;
            var sign = request.Kind == "credit" ? -1m : 1m;
            foreach (var g in request.Postings.Where(p => stockAccounts.Contains(p.DebitAccount)).GroupBy(p => p.DebitAccount))
            {
                facts.Add(new AgnumDocFact(row.DocumentType, row.DocumentId, row.DocumentNumber, row.DocumentDate,
                    row.Status == OutboxStatus.Sent ? AgnumDocState.Sent : AgnumDocState.Unsent, g.Key, sign * g.Sum(p => p.AmountEur)));
            }
        }

        foreach (var s in await _db.InboundShipments.AsNoTracking()
                     .Where(s => s.PostingStatus == "Posted" && s.AccountingRoute == "ManualInAgnum")
                     .Select(s => new { s.Id, s.InvoiceNumber, s.InvoiceDate, s.ReferenceNumber }).ToListAsync(ct))
            facts.Add(new AgnumDocFact(OutboxDocumentType.Shipment, s.Id, s.InvoiceNumber ?? s.ReferenceNumber, s.InvoiceDate ?? DateOnly.MinValue, AgnumDocState.Manual, null, 0m));
        foreach (var d in await _db.AdditionalCostDocuments.AsNoTracking()
                     .Where(d => d.PostingStatus == "Posted" && d.AccountingRoute == "ManualInAgnum")
                     .Select(d => new { d.Id, d.DocumentNumber, d.DocumentDate }).ToListAsync(ct))
            facts.Add(new AgnumDocFact(OutboxDocumentType.CostDocument, d.Id, d.DocumentNumber, d.DocumentDate, AgnumDocState.Manual, null, 0m));
        foreach (var c in await _db.SupplierCreditDocuments.AsNoTracking()
                     .Where(c => c.PostingStatus == "Posted" && c.AccountingRoute == "ManualInAgnum")
                     .Select(c => new { c.Id, c.CreditNumber, c.CreditDate }).ToListAsync(ct))
            facts.Add(new AgnumDocFact(OutboxDocumentType.SupplierCredit, c.Id, c.CreditNumber, c.CreditDate, AgnumDocState.Manual, null, 0m));
        return facts;
    }

    public async Task<bool> IsClosedAsync(int year, int month, CancellationToken ct)
        => await _db.AccountingPeriods.AsNoTracking().AnyAsync(p => p.Year == year && p.Month == month && p.Status == PeriodStatus.Closed, ct);

    /// <summary>Reasons the month cannot be closed now (empty = it can).</summary>
    public async Task<IReadOnlyList<string>> BlockersAsync(int year, int month, MonthEndReport? report, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var blockers = new List<string>();
        if (to >= Today) blockers.Add($"The month {year}-{month:00} has not ended yet.");
        var open = await _db.AgnumOutbox.AsNoTracking()
            .Where(r => r.DocumentDate >= from && r.DocumentDate <= to && r.Status != OutboxStatus.Sent && r.Status != OutboxStatus.Annulled)
            .OrderBy(r => r.Id).Select(r => new { r.DocumentNumber, r.Status, r.Operation }).ToListAsync(ct);
        blockers.AddRange(open.Select(r => $"{r.DocumentNumber}: {r.Operation} is {r.Status}."));
        report ??= await BuildLiveAsync(year, month, ct);
        foreach (var b in report.Bridge.Where(b => b.Unexplained != 0m))
            blockers.Add($"Stock account {b.StockAccount}: unexplained difference to Agnum {b.Unexplained:0.00} EUR.");
        foreach (var r in report.Statement.Where(r => r.Residual != 0m))
            blockers.Add($"Stock account {r.StockAccount}: statement residual {r.Residual:0.00} EUR.");
        return blockers;
    }

    public async Task<PeriodStateDto> StateAsync(int year, int month, CancellationToken ct)
    {
        var p = await _db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Year == year && x.Month == month, ct);
        var blockers = p?.Status == PeriodStatus.Closed ? [] : await BlockersAsync(year, month, null, ct);
        return new PeriodStateDto(year, month, p?.Status ?? PeriodStatus.Open, p?.ClosedAt, p?.ClosedBy, p?.ReopenedAt, p?.ReopenedBy, p?.ReopenReason, blockers);
    }

    public async Task CloseAsync(int year, int month, string user, CancellationToken ct)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new MonthEndException("Invalid month.");
        if (await IsClosedAsync(year, month, ct)) throw new MonthEndException("The month is already closed.");
        var report = await BuildLiveAsync(year, month, ct);
        var blockers = await BlockersAsync(year, month, report, ct);
        if (blockers.Count > 0) throw new MonthEndException("The month cannot be closed.", blockers);

        var now = _time.GetUtcNow();
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is null)
        {
            period = new AccountingPeriod { Year = year, Month = month };
            _db.AccountingPeriods.Add(period);
        }

        period.Status = PeriodStatus.Closed;
        period.ClosedAt = now;
        period.ClosedBy = user;
        var old = await _db.MonthEndSnapshots.Where(s => s.Year == year && s.Month == month).ToListAsync(ct);
        _db.MonthEndSnapshots.RemoveRange(old);
        _db.MonthEndSnapshots.Add(new MonthEndSnapshot { Year = year, Month = month, ReportJson = JsonSerializer.Serialize(report, Json), CreatedAt = now });
        Audit(year, month, "Close", user, now, null);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReopenAsync(int year, int month, string reason, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new MonthEndException("A reason is required to reopen a month.");
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is not { Status: PeriodStatus.Closed }) throw new MonthEndException("The month is not closed.");
        var now = _time.GetUtcNow();
        period.Status = PeriodStatus.Open;
        period.ReopenedAt = now;
        period.ReopenedBy = user;
        period.ReopenReason = reason.Trim();
        _db.MonthEndSnapshots.RemoveRange(await _db.MonthEndSnapshots.Where(s => s.Year == year && s.Month == month).ToListAsync(ct));
        Audit(year, month, "Reopen", user, now, reason.Trim());
        await _db.SaveChangesAsync(ct);
    }

    private void Audit(int year, int month, string action, string user, DateTimeOffset now, string? reason)
        => _db.AccountingConfigAudit.Add(new AccountingConfigAuditEntry
        {
            Entity = "AccountingPeriod", EntityId = $"{year}-{month:00}", Action = action, ChangedBy = user, ChangedAt = now,
            BeforeJson = JsonSerializer.Serialize(new { Status = action == "Close" ? PeriodStatus.Open : PeriodStatus.Closed }, Json),
            AfterJson = JsonSerializer.Serialize(new { Status = action == "Close" ? PeriodStatus.Closed : PeriodStatus.Open, reason }, Json)
        });
}
