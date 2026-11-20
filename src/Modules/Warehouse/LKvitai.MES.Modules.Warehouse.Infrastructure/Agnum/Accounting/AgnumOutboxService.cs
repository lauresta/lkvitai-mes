using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

public sealed record OutboxRowDto(
    long Id, string DocumentType, int DocumentId, string DocumentNumber, DateOnly DocumentDate, string Currency, decimal GrossAmount,
    int SupplierId, string? SupplierName, string Operation, int Revision, string Status, int Attempts, string? LastError,
    DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset NextAttemptAt, long? AgnumDocumentId, bool Held);

public sealed record SendingSettingsDto(bool Enabled, DateOnly? StartDate, string? ServiceUrl,
    DateTimeOffset? LastReconciledAt, int LastReconciliationFindings, string? LastReconciliationError,
    int Waiting, int NeedsReview, DateTimeOffset? OldestOpenAt);

public sealed record FindingDto(long Id, DateTimeOffset RunAt, string Kind, string? DocumentType, int? DocumentId, string? DocumentNumber, string Details);

public enum UnpostPlan
{
    /// <summary>Nothing reached Agnum (or the document is manual): reverse locally right away.</summary>
    ReverseNow,
    /// <summary>An annul row was queued; the sender reverses the document after Agnum confirmed.</summary>
    AnnulQueued,
    AlreadyQueued
}

public sealed class OutboxActionException : Exception
{
    public OutboxActionException(string message) : base(message) { }
}

/// <summary>Queue screens of blueprint 4.7: list, retry, mark manual, settings, un-post planning. No stock value work here.</summary>
public sealed class AgnumOutboxService
{
    private static readonly JsonSerializerOptions Json = new();
    private readonly WarehouseDbContext _db;
    private readonly TimeProvider _time;

    public AgnumOutboxService(WarehouseDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<AgnumSendingSettings> GetSettingsAsync(CancellationToken ct)
        => await _db.AgnumSendingSettings.FirstOrDefaultAsync(ct) ?? new AgnumSendingSettings();

    public async Task<SendingSettingsDto> GetSettingsDtoAsync(string? serviceUrl, CancellationToken ct)
    {
        var s = await _db.AgnumSendingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new AgnumSendingSettings();
        var open = _db.AgnumOutbox.AsNoTracking().Where(r => r.Status != OutboxStatus.Sent && r.Status != OutboxStatus.Annulled);
        var oldest = await open.OrderBy(r => r.CreatedAt).Select(r => (DateTimeOffset?)r.CreatedAt).FirstOrDefaultAsync(ct);
        return new SendingSettingsDto(s.Enabled, s.StartDate, serviceUrl, s.LastReconciledAt, s.LastReconciliationFindings,
            s.LastReconciliationError, await open.CountAsync(ct), await open.CountAsync(r => r.Status == OutboxStatus.NeedsReview, ct), oldest);
    }

    public async Task SaveSettingsAsync(bool enabled, DateOnly? startDate, string user, CancellationToken ct)
    {
        if (enabled && startDate is null)
            throw new OutboxActionException("A start date is required before sending is switched on.");

        var settings = await _db.AgnumSendingSettings.FirstOrDefaultAsync(ct);
        var before = settings is null ? null : JsonSerializer.Serialize(new { settings.Enabled, settings.StartDate }, Json);
        if (settings is null)
        {
            settings = new AgnumSendingSettings();
            _db.AgnumSendingSettings.Add(settings);
        }

        settings.Enabled = enabled;
        settings.StartDate = startDate;
        settings.UpdatedAt = _time.GetUtcNow();
        settings.UpdatedBy = user;
        _db.AccountingConfigAudit.Add(new AccountingConfigAuditEntry
        {
            Entity = "AgnumSendingSettings", EntityId = "1", Action = before is null ? "Create" : "Update", ChangedBy = user,
            ChangedAt = _time.GetUtcNow(), BeforeJson = before ?? "null", AfterJson = JsonSerializer.Serialize(new { enabled, startDate }, Json)
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxRowDto>> ListAsync(string? status, bool openOnly, int take, CancellationToken ct)
    {
        var settings = await _db.AgnumSendingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var query = _db.AgnumOutbox.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        else if (openOnly) query = query.Where(r => r.Status != OutboxStatus.Sent && r.Status != OutboxStatus.Annulled);
        var rows = await query.OrderByDescending(r => r.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
        var names = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return rows.Select(r => ToDto(r, names.GetValueOrDefault(r.SupplierId), settings)).ToList();
    }

    public async Task<IReadOnlyList<OutboxRowDto>> ForDocumentAsync(string documentType, int documentId, CancellationToken ct)
    {
        var settings = await _db.AgnumSendingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var rows = await _db.AgnumOutbox.AsNoTracking().Where(r => r.DocumentType == documentType && r.DocumentId == documentId)
            .OrderByDescending(r => r.Id).Take(10).ToListAsync(ct);
        return rows.Select(r => ToDto(r, null, settings)).ToList();
    }

    private static OutboxRowDto ToDto(AgnumDocumentOutbox r, string? supplier, AgnumSendingSettings? s) => new(
        r.Id, r.DocumentType, r.DocumentId, r.DocumentNumber, r.DocumentDate, r.Currency, r.GrossAmount, r.SupplierId, supplier,
        r.Operation, r.Revision, r.Status, r.Attempts, r.LastError, r.CreatedAt, r.SentAt, r.NextAttemptAt, r.AgnumDocumentId,
        Held: !OutboxStatus.IsTerminal(r.Status) && (s is not { Enabled: true } || (s.StartDate is { } d && r.DocumentDate < d)));

    public async Task<IReadOnlyList<FindingDto>> FindingsAsync(CancellationToken ct)
        => await _db.AgnumReconciliationFindings.AsNoTracking().OrderBy(f => f.Id)
            .Select(f => new FindingDto(f.Id, f.RunAt, f.Kind, f.DocumentType, f.DocumentId, f.DocumentNumber, f.Details)).ToListAsync(ct);

    /// <summary>"Retry": the row goes back to the queue and its attempts start from zero; the sender begins with GET.</summary>
    public async Task RetryAsync(long id, CancellationToken ct)
    {
        var row = await _db.AgnumOutbox.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException($"Outbox row {id} does not exist.");
        if (row.Status is not (OutboxStatus.NeedsReview or OutboxStatus.Unknown or OutboxStatus.Pending))
            throw new OutboxActionException($"A row in status {row.Status} cannot be retried.");

        row.Status = OutboxStatus.Pending;
        row.Attempts = 0;
        row.NextAttemptAt = _time.GetUtcNow();
        row.LeasedUntil = null;
        row.Version++;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>"Mark manual": the accountant entered the document in Agnum himself; the row leaves the queue (blueprint 4.8).</summary>
    public async Task MarkManualAsync(long id, string reason, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new OutboxActionException("A reason is required.");
        var row = await _db.AgnumOutbox.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException($"Outbox row {id} does not exist.");
        if (row.Operation != OutboxOperation.Post || row.Status is OutboxStatus.Sent or OutboxStatus.Annulled)
            throw new OutboxActionException("Only an unsent posting can be marked as entered manually.");

        row.Status = OutboxStatus.Annulled;
        row.LastError = $"Marked as entered manually in Agnum by {user}: {reason.Trim()}";
        row.LeasedUntil = null;
        row.Version++;
        if (row.DocumentType == OutboxDocumentType.Shipment)
        {
            var s = await _db.InboundShipments.FirstOrDefaultAsync(x => x.Id == row.DocumentId, ct);
            if (s is not null) { s.AccountingRoute = "ManualInAgnum"; s.ManualReason = reason.Trim(); }
        }
        else
        {
            var d = await _db.AdditionalCostDocuments.FirstOrDefaultAsync(x => x.Id == row.DocumentId, ct);
            if (d is not null) { d.AccountingRoute = "ManualInAgnum"; d.ManualReason = reason.Trim(); }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Decides how a posted document is un-posted. An unsent Pending row is simply withdrawn together with the
    /// reversal (see <see cref="WithdrawPostRowsAsync"/>); anything that may have reached Agnum needs an Annul row.
    /// </summary>
    public async Task<UnpostPlan> PlanUnpostAsync(string documentType, int documentId, int supplierId, string route, Guid? key,
        int revision, string? number, DateOnly date, string currency, decimal gross, string user, CancellationToken ct)
    {
        if (route == "ManualInAgnum" || key is null) return UnpostPlan.ReverseNow;
        var rows = await _db.AgnumOutbox.Where(r => r.Key == key && r.Revision == revision).OrderBy(r => r.Id).ToListAsync(ct);
        if (rows.Any(r => r.Operation == OutboxOperation.Annul && !OutboxStatus.IsTerminal(r.Status))) return UnpostPlan.AlreadyQueued;
        var post = rows.LastOrDefault(r => r.Operation == OutboxOperation.Post);
        if (post is null || post.Status == OutboxStatus.Annulled) return UnpostPlan.ReverseNow;
        if (post.Status == OutboxStatus.Pending && post.Attempts == 0) return UnpostPlan.ReverseNow;

        var annulExists = rows.Any(r => r.Operation == OutboxOperation.Annul);
        if (annulExists) throw new OutboxActionException("An annulment of this posting was already tried; retry it from the queue.");

        if (post.Status != OutboxStatus.Sent)
        {
            // It may or may not have reached Agnum; the Annul row looks it up (GET) before it deletes anything.
            post.Status = OutboxStatus.Annulled;
            post.LastError = "Superseded by an annulment.";
            post.LeasedUntil = null;
            post.Version++;
        }

        _db.AgnumOutbox.Add(new AgnumDocumentOutbox
        {
            Key = key.Value, Revision = revision, DocumentType = documentType, DocumentId = documentId, SupplierId = supplierId,
            DocumentNumber = number ?? string.Empty, DocumentDate = date, Currency = currency, GrossAmount = gross,
            Operation = OutboxOperation.Annul, PayloadJson = JsonSerializer.Serialize(new { key, revision }, Json),
            PayloadHash = post.PayloadHash, Status = OutboxStatus.Pending, NextAttemptAt = _time.GetUtcNow(),
            CreatedAt = _time.GetUtcNow(), CreatedBy = user
        });
        await _db.SaveChangesAsync(ct);
        return UnpostPlan.AnnulQueued;
    }

    /// <summary>Called inside the reversal: the posting rows of this revision are no longer wanted.</summary>
    public async Task WithdrawPostRowsAsync(Guid key, int revision, CancellationToken ct)
    {
        var rows = await _db.AgnumOutbox
            .Where(r => r.Key == key && r.Revision == revision && r.Operation == OutboxOperation.Post && r.Status != OutboxStatus.Annulled)
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            r.Status = OutboxStatus.Annulled;
            r.LastError ??= "Un-posted.";
            r.LeasedUntil = null;
            r.Version++;
        }
    }
}
