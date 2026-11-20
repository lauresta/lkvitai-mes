using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

/// <summary>Puts a posted document back to NotPosted and restores its stock value. Must be idempotent.</summary>
public interface IPostingReverser
{
    Task ReverseAsync(string documentType, int documentId, string user, CancellationToken ct);
}

public sealed record SenderRunResult(int Sent, int Retry, int Review, int Skipped);

/// <summary>
/// Blueprint 4.7 sender. Row claim: optimistic version token, so two sender instances cannot take one row.
/// GET before every POST: a timeout after the commit never produces a second document.
/// </summary>
public sealed class AgnumOutboxSender
{
    public static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    public const int BatchSize = 50;
    public const string SenderUser = "agnum-sender";

    private readonly WarehouseDbContext _db;
    private readonly IAgnumPostingClient _client;
    private readonly IPostingReverser _reverser;
    private readonly TimeProvider _time;
    private readonly ILogger<AgnumOutboxSender> _logger;

    public AgnumOutboxSender(WarehouseDbContext db, IAgnumPostingClient client, IPostingReverser reverser,
        ILogger<AgnumOutboxSender> logger, TimeProvider? time = null)
    {
        _db = db;
        _client = client;
        _reverser = reverser;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<SenderRunResult> RunOnceAsync(CancellationToken ct)
    {
        var settings = await _db.AgnumSendingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not { Enabled: true }) return new(0, 0, 0, 0);

        var now = _time.GetUtcNow();
        await ReleaseExpiredLeasesAsync(now, ct);

        var candidates = await _db.AgnumOutbox
            .Where(r => (r.Status == OutboxStatus.Pending || r.Status == OutboxStatus.Unknown) && r.NextAttemptAt <= now)
            .OrderBy(r => r.Id).Take(BatchSize).ToListAsync(ct);

        int sent = 0, retry = 0, review = 0, skipped = 0;
        foreach (var row in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (settings.StartDate is { } start && row.DocumentDate < start) { skipped++; continue; }
            if (await IsBlockedAsync(row, settings.StartDate, ct)) { skipped++; continue; }
            if (!await TryClaimAsync(row, now, ct)) { skipped++; continue; }

            await ProcessAsync(row, ct);
            switch (row.Status)
            {
                case OutboxStatus.Sent: sent++; break;
                case OutboxStatus.NeedsReview: review++; break;
                default: retry++; break;
            }
        }

        return new(sent, retry, review, skipped);
    }

    private async Task ReleaseExpiredLeasesAsync(DateTimeOffset now, CancellationToken ct)
    {
        var stale = await _db.AgnumOutbox.Where(r => r.Status == OutboxStatus.Sending && r.LeasedUntil != null && r.LeasedUntil < now).ToListAsync(ct);
        foreach (var row in stale)
        {
            row.Status = OutboxStatus.Unknown;
            row.LastError = "The sender stopped before it finished; the next attempt starts with GET.";
            row.NextAttemptAt = now;
            row.LeasedUntil = null;
            row.Version++;
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { _db.Entry(row).State = EntityState.Detached; }
        }
    }

    /// <summary>Per supplier strictly in posting order: an earlier row that is not Sent/Annulled holds the later ones back.</summary>
    private Task<bool> IsBlockedAsync(AgnumDocumentOutbox row, DateOnly? start, CancellationToken ct)
        => _db.AgnumOutbox.AsNoTracking().AnyAsync(o =>
            o.SupplierId == row.SupplierId && o.Id < row.Id
            && o.Status != OutboxStatus.Sent && o.Status != OutboxStatus.Annulled
            && (start == null || o.DocumentDate >= start), ct);

    private async Task<bool> TryClaimAsync(AgnumDocumentOutbox row, DateTimeOffset now, CancellationToken ct)
    {
        row.Status = OutboxStatus.Sending;
        row.LeasedUntil = now + Lease;
        row.Attempts++;
        row.Version++;
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(row).State = EntityState.Detached;
            return false;
        }
    }

    private async Task ProcessAsync(AgnumDocumentOutbox row, CancellationToken ct)
    {
        try
        {
            if (row.Operation == OutboxOperation.Annul) await SendAnnulAsync(row, ct);
            else await SendPostAsync(row, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishAsync(row, OutboxStatus.Unknown, "Cancelled while sending.", TimeSpan.Zero, CancellationToken.None);
            throw;
        }
        catch (AgnumPostingHttpException ex)
        {
            await FailHttpAsync(row, ex.Status, ex.Message, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            // The request may have been committed by the service; the next attempt starts with GET.
            _logger.LogWarning(ex, "Agnum outbox row {Row} ended without an answer.", row.Id);
            await FinishAsync(row, OutboxStatus.Unknown, Trim(ex.Message), BackoffFor(row.Attempts), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agnum outbox row {Row} failed.", row.Id);
            await FinishAsync(row, OutboxStatus.NeedsReview, Trim(ex.Message), TimeSpan.Zero, ct);
        }
    }

    private async Task SendPostAsync(AgnumDocumentOutbox row, CancellationToken ct)
    {
        var lookup = await _client.GetByKeyAsync(row.Key, ct);
        if (lookup.Found)
        {
            var d = lookup.Document!;
            if (d.Revision == row.Revision && string.Equals(d.Hash, row.PayloadHash, StringComparison.OrdinalIgnoreCase))
            {
                await MarkSentAsync(row, d.IdDok, d.IdOp, d.Postings is null ? null : JsonSerializer.Serialize(d.Postings), ct);
                return;
            }

            await FinishAsync(row, OutboxStatus.NeedsReview,
                $"Agnum already holds this key with revision {d.Revision} and a different content (id_dok {d.IdDok}).", TimeSpan.Zero, ct);
            return;
        }

        var result = await _client.PostAsync(row.PayloadJson, ct);
        if (result.Ok)
        {
            await MarkSentAsync(row, result.IdDok, result.IdOp, result.PostingsJson, ct);
            return;
        }

        await FailHttpAsync(row, result.Status, result.Message ?? $"HTTP {result.Status}", ct);
    }

    private async Task SendAnnulAsync(AgnumDocumentOutbox row, CancellationToken ct)
    {
        var lookup = await _client.GetByKeyAsync(row.Key, ct);
        if (lookup.Found)
        {
            var revision = lookup.Document!.Revision ?? row.Revision;
            var result = await _client.DeleteAsync(row.Key, revision, ct);
            if (!result.Ok && result.Status != 404)
            {
                await FailHttpAsync(row, result.Status, result.Message ?? $"HTTP {result.Status}", ct);
                return;
            }
        }

        // Gone from Agnum (deleted now or never there): bring the document back to NotPosted.
        try
        {
            await _reverser.ReverseAsync(row.DocumentType, row.DocumentId, SenderUser, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Annulment of {Type} {Id} left Agnum, but the local reversal failed.", row.DocumentType, row.DocumentId);
            await FinishAsync(row, OutboxStatus.NeedsReview, "Removed in Agnum, local reversal failed: " + Trim(ex.Message), TimeSpan.Zero, ct);
            return;
        }

        await MarkSentAsync(row, null, null, null, ct);
        var older = await _db.AgnumOutbox
            .Where(r => r.Key == row.Key && r.Revision == row.Revision && r.Operation == OutboxOperation.Post && r.Status != OutboxStatus.Annulled)
            .ToListAsync(ct);
        foreach (var p in older) { p.Status = OutboxStatus.Annulled; p.Version++; }
        await _db.SaveChangesAsync(ct);
    }

    private Task FailHttpAsync(AgnumDocumentOutbox row, int status, string message, CancellationToken ct)
    {
        var text = Trim($"HTTP {status}: {message}");
        var transient = status >= 500 || status is 401 or 403 or 408 or 429;
        if (!transient)
            return FinishAsync(row, OutboxStatus.NeedsReview, text, TimeSpan.Zero, ct);

        // 5xx: wait 1, 5, 15, 60 minutes, then a person decides.
        return row.Attempts > Backoff.Length
            ? FinishAsync(row, OutboxStatus.NeedsReview, text, TimeSpan.Zero, ct)
            : FinishAsync(row, OutboxStatus.Pending, text, BackoffFor(row.Attempts), ct);
    }

    public static TimeSpan BackoffFor(int attempts) => Backoff[Math.Clamp(attempts - 1, 0, Backoff.Length - 1)];

    private async Task MarkSentAsync(AgnumDocumentOutbox row, long? idDok, long? idOp, string? postingsJson, CancellationToken ct)
    {
        row.Status = OutboxStatus.Sent;
        row.AgnumDocumentId = idDok ?? row.AgnumDocumentId;
        row.AgnumOperationId = idOp ?? row.AgnumOperationId;
        row.AppliedPostingsJson = postingsJson ?? row.AppliedPostingsJson;
        row.SentAt = _time.GetUtcNow();
        row.LastError = null;
        row.LeasedUntil = null;
        row.Version++;
        await _db.SaveChangesAsync(ct);
    }

    private async Task FinishAsync(AgnumDocumentOutbox row, string status, string error, TimeSpan delay, CancellationToken ct)
    {
        row.Status = status;
        row.LastError = error;
        row.LeasedUntil = null;
        row.NextAttemptAt = _time.GetUtcNow() + delay;
        row.Version++;
        await _db.SaveChangesAsync(ct);
    }

    private static string Trim(string s) => s.Length > 1900 ? s[..1900] : s;
}
