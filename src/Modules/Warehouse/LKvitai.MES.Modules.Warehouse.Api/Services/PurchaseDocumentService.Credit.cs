using System.Globalization;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>Blueprint 4.8: supplier credit notes and supplier returns, posted like a purchase document.</summary>
public sealed partial class PurchaseDocumentService
{
    public const string ReturnDestination = "RETURN_TO_SUPPLIER";

    private Task<SupplierCreditDocument?> LoadCreditAsync(int id, CancellationToken ct)
        => _db.SupplierCreditDocuments
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.OriginalLine)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>Creates the credit (draft) and optionally posts it; when posting is requested and a check fails nothing is saved.</summary>
    public async Task<(SupplierCreditDocument? Credit, IReadOnlyList<PostingProblem> Problems, bool NotFound)> CreateCreditAsync(
        SupplierCreditDocument credit, bool postNow, string user, CancellationToken ct)
    {
        var original = await LoadAsync(credit.OriginalShipmentId, ct);
        if (original is null)
        {
            return (null, [], true);
        }

        credit.SupplierId = original.SupplierId;
        credit.Currency = original.Currency;
        credit.OriginalInvoiceNumber = original.InvoiceNumber;
        credit.OriginalInvoiceDate = original.InvoiceDate;
        credit.AccountingRoute = original.AccountingRoute;
        credit.VatTreatment = original.VatTreatment;
        credit.GrossAmount = credit.NetAmount + credit.VatAmount;
        credit.CreatedBy = user;
        credit.CreatedAt = DateTimeOffset.UtcNow;
        foreach (var line in credit.Lines)
        {
            var source = original.Lines.FirstOrDefault(l => l.Id == line.OriginalLineId);
            if (source is not null)
            {
                line.ItemId = source.ItemId;
                line.Item = source.Item;
                line.OriginalLine = source;
            }
        }

        if (postNow)
        {
            var problems = await ValidateCreditAsync(credit, original, ct);
            if (problems.Count > 0)
            {
                return (null, problems, false);
            }
        }

        _db.SupplierCreditDocuments.Add(credit);
        // Saved first so the queue row can refer to the credit id; posting re-validates under the lock.
        await _db.SaveChangesAsync(ct);
        if (!postNow)
        {
            return (credit, [], false);
        }

        var result = await PostCreditAsync(credit, original, user, ct);
        return (credit, result.Problems, false);
    }

    public async Task<PostResult> PostCreditAsync(int id, string user, CancellationToken ct)
    {
        var credit = await LoadCreditAsync(id, ct);
        if (credit is null)
        {
            return new PostResult(true, []);
        }

        var original = await LoadAsync(credit.OriginalShipmentId, ct);
        return await PostCreditAsync(credit, original!, user, ct);
    }

    public async Task<IReadOnlyList<PostingProblem>> DryRunCreditAsync(int id, CancellationToken ct)
    {
        var credit = await LoadCreditAsync(id, ct);
        if (credit is null || credit.PostingStatus == "Posted")
        {
            return [];
        }

        return await ValidateCreditAsync(credit, await LoadAsync(credit.OriginalShipmentId, ct), ct);
    }

    private async Task<PostResult> PostCreditAsync(SupplierCreditDocument credit, InboundShipment original, string user, CancellationToken ct)
    {
        var keys = new List<long> { StockLockKey.ForLocation(_warehouse.Code, "INBOUND", original.Id.ToString(CultureInfo.InvariantCulture)) };
        foreach (var line in credit.Lines.Where(l => l.ReturnQty > 0m && l.Item is not null && !string.IsNullOrWhiteSpace(l.FromLocation)))
        {
            keys.Add(StockLockKey.ForLocation(_warehouse.Code, line.FromLocation!.Trim(), line.Item!.InternalSKU));
        }

        await using var guard = await _locks.CreateAsync(ct);
        await guard.AcquireAsync(keys.Distinct().OrderBy(k => k).ToArray(), ct);

        var problems = await ValidateCreditAsync(credit, original, ct);
        if (problems.Count > 0)
        {
            return new PostResult(false, problems);
        }

        var rate = await ResolveRateAsync(credit.Currency, credit.CreditDate, null, null, ct);
        var profileIds = await ResolveItemProfilesAsync(credit.Lines.Select(l => l.ItemId).Distinct().ToList(), ct);
        var requests = ApplyCredit(credit, rate, user, profileIds.Values.FirstOrDefault());
        if (credit.AccountingRoute == "Auto")
        {
            await QueueCreditPostingAsync(credit, original, profileIds.Values.FirstOrDefault(), user, ct);
        }

        var movements = credit.Lines.Where(l => l.ReturnQty > 0m && l.Item is not null)
            .Select(l => (Stream: StockLedgerStreamId.For(_warehouse.Code, l.FromLocation!.Trim(), l.Item!.InternalSKU),
                Event: new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = l.Item!.InternalSKU,
                    Quantity = l.ReturnQty,
                    FromLocation = l.FromLocation!.Trim(),
                    ToLocation = ReturnDestination,
                    MovementType = "SupplierReturn",
                    Reason = $"Credit {credit.CreditNumber} for {original.ReferenceNumber}",
                    Timestamp = DateTime.UtcNow
                })).ToList();

        IReadOnlyList<StockValueChanged> written = [];
        await StockValueRetry.RunAsync(() => _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await using var session = _store.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (NpgsqlTransaction)transaction.GetDbTransaction(), false));
            await _db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            foreach (var movement in movements)
            {
                session.Events.Append(movement.Stream, movement.Event);
            }

            written = await StockValueWriter.AppendAsync(session, _db, requests, ct);
            await session.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _db.ChangeTracker.AcceptAllChanges();
        }), ct);
        await StockValueWriter.ProjectAsync(_db, _logger, written, ct);
        await guard.CommitAsync(ct);
        return new PostResult(false, []);
    }

    /// <summary>
    /// Blueprint 4.8: converts the amounts to EUR, sets the negative Agnum amounts and the posting fields, and returns
    /// one value request per stock line. Lines of Service items carry no stock value.
    /// </summary>
    public static List<StockValueRequest> ApplyCredit(SupplierCreditDocument credit, PostingRate rate, string user, int? profileId)
    {
        var eurPerUnit = PurchaseMath.RateEurPerUnit(rate.UnitsPerEur!.Value);
        credit.RateUnitsPerEur = rate.UnitsPerEur;
        credit.RateDate = rate.RateDate;
        credit.RateSource = rate.Source;
        credit.GrossAmount = credit.NetAmount + credit.VatAmount;
        credit.NetEur = PurchaseMath.ToEur(credit.NetAmount, eurPerUnit);
        credit.VatEur = PurchaseMath.ToEur(credit.VatAmount, eurPerUnit);
        credit.GrossEur = credit.NetEur + credit.VatEur;
        credit.AgnumNetEur = -credit.NetEur;
        credit.AgnumVatEur = -credit.VatEur;
        credit.AgnumGrossEur = -credit.GrossEur;

        var lines = credit.Lines.OrderBy(l => l.Id).ToList();
        var shares = PurchaseMath.Allocate(credit.NetEur.Value, lines.Select(l => l.NetAmount).ToList());
        var document = new StockValueDocument(
            "SupplierCreditDocument",
            credit.Id.ToString(CultureInfo.InvariantCulture),
            credit.CreditNumber,
            $"Credit {credit.CreditNumber} for invoice {credit.OriginalInvoiceNumber}",
            profileId);
        var requests = new List<StockValueRequest>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            line.NetEur = shares[i];
            if (line.Item is null || line.Item.ItemType == ItemType.Service)
            {
                continue;
            }

            requests.Add(StockValueRequest.ForSupplierCredit(
                line.Item.InternalSKU, line.ReturnQty, shares[i], line.OriginalLine?.ReceivedQty ?? 0m, document));
        }

        credit.PostingStatus = "Posted";
        credit.PostedAt = DateTimeOffset.UtcNow;
        credit.PostedBy = user;
        credit.PostingRevision++;
        return requests;
    }

    /// <summary>Blueprint 4.8: a posted credit goes to Agnum as kind "credit" (positive sums, stored negative there) with the original number and date.</summary>
    private async Task QueueCreditPostingAsync(SupplierCreditDocument credit, InboundShipment original, int? profileId, string user, CancellationToken ct)
    {
        if (profileId is null)
        {
            throw new InvalidOperationException("The credit has no stock account profile; it cannot be queued for Agnum.");
        }

        var supplier = await _db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == credit.SupplierId, ct);
        var treatment = credit.VatTreatment ?? PurchasePostingValidator.EffectiveVatTreatment(original, supplier)
            ?? throw new InvalidOperationException("VAT treatment is required.");
        var profile = await _db.StockAccountProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId.Value, ct);
        var rule = await _db.AgnumPostingRules.AsNoTracking()
            .FirstAsync(r => r.StockAccountProfileId == profileId.Value && r.VatTreatment == treatment, ct);
        credit.AgnumKey ??= Guid.NewGuid();
        _db.AgnumOutbox.Add(CreditOutboxRow(credit, supplier.AgnumClientId!.Value, profile.AgnumSndId, rule.SchemeId, rule.VatCode, profile.StockAccount, user));
    }

    /// <summary>The outbox row of a posted credit: positive sums with kind credit (Agnum stores them negative), the queue shows the gross negative.</summary>
    public static AgnumDocumentOutbox CreditOutboxRow(SupplierCreditDocument credit, long clientId, long sndId, long schemeId,
        string? vatCode, string stockAccount, string user)
    {
        var payload = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            credit.AgnumKey!.Value, credit.PostingRevision, credit.CreditNumber, credit.CreditDate,
            clientId, sndId, schemeId, credit.Currency, credit.RateUnitsPerEur!.Value,
            credit.NetAmount, credit.VatAmount, vatCode, stockAccount, [],
            Kind: "credit", RefDocNo: credit.OriginalInvoiceNumber, RefDocDate: credit.OriginalInvoiceDate));
        return NewRow(payload, OutboxDocumentType.SupplierCredit, credit.Id, credit.SupplierId, credit.CreditNumber,
            credit.CreditDate, credit.Currency, -credit.GrossAmount, user);
    }

    public async Task<List<PostingProblem>> ValidateCreditAsync(SupplierCreditDocument credit, InboundShipment? original, CancellationToken ct)
    {
        var problems = new List<PostingProblem>();
        if (credit.PostingStatus == "Posted")
        {
            problems.Add(new("AlreadyPosted", "The document is already posted."));
            return problems;
        }

        if (original is null || original.PostingStatus != "Posted")
        {
            problems.Add(new("OriginalNotPosted", "The original purchase document must be posted first."));
            return problems;
        }

        if (string.IsNullOrWhiteSpace(credit.CreditNumber)) problems.Add(new("InvoiceNumberRequired", "Credit note number is required."));
        if (credit.NetAmount <= 0m) problems.Add(new("AmountsRequired", "Net amount must be greater than zero."));
        if (credit.VatAmount < 0m) problems.Add(new("AmountsRequired", "VAT amount cannot be negative."));
        if (credit.Lines.Count == 0) problems.Add(new("NoStockLines", "The document has no lines."));

        var supplier = await _db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == original.SupplierId, ct);
        if (credit.AccountingRoute == "Auto")
        {
            var check = AgnumSupplierSyncService.CheckPostable(supplier);
            if (!check.Allowed) problems.Add(new("SupplierNotPostable", check.Reason ?? "Supplier cannot be posted."));
        }

        // Check 1: exact equality of the lines and the net amount in the document currency.
        if (credit.Lines.Sum(l => l.NetAmount) != credit.NetAmount)
            problems.Add(new("LinesTotalMismatch",
                $"Lines total {credit.Lines.Sum(l => l.NetAmount):0.00} {credit.Currency}, but the net amount is {credit.NetAmount:0.00} {credit.Currency}."));

        var hasReturn = credit.Lines.Any(l => l.ReturnQty > 0m);
        if (credit.DocumentKind == SupplierCreditDocument.KindSupplierReturn && !hasReturn)
            problems.Add(new("ReturnQtyRequired", "A supplier return needs a returned quantity."));
        if (credit.DocumentKind == SupplierCreditDocument.KindCreditNote && hasReturn)
            problems.Add(new("ReturnQtyNotAllowed", "A price-only credit note cannot return goods; use a supplier return."));

        var priorLines = await _db.SupplierCreditLines.AsNoTracking()
            .Where(l => l.Credit!.OriginalShipmentId == original.Id && l.Credit.PostingStatus == "Posted" && l.CreditId != credit.Id)
            .Select(l => new { l.OriginalLineId, l.ReturnQty }).ToListAsync(ct);
        foreach (var line in credit.Lines)
        {
            var source = original.Lines.FirstOrDefault(l => l.Id == line.OriginalLineId);
            if (source is null)
            {
                problems.Add(new("LineNotInOriginal", $"Line {line.OriginalLineId} does not belong to the original document."));
                continue;
            }

            if (line.ReturnQty < 0m || line.NetAmount < 0m)
            {
                problems.Add(new("AmountsRequired", "Quantities and amounts cannot be negative."));
                continue;
            }

            if (line.ReturnQty <= 0m)
            {
                continue;
            }

            var returned = priorLines.Where(p => p.OriginalLineId == source.Id).Sum(p => p.ReturnQty);
            if (line.ReturnQty > source.ReceivedQty - returned)
                problems.Add(new("ReturnQtyExceeded", $"Only {source.ReceivedQty - returned:0.####} of {source.Item?.InternalSKU} can still be returned."));
            if (string.IsNullOrWhiteSpace(line.FromLocation)
                || !await _db.Locations.AnyAsync(x => x.Code == line.FromLocation.Trim(), ct))
            {
                problems.Add(new("ReturnLocationRequired", "Choose an existing location the goods are returned from."));
            }
            else if (source.Item is not null)
            {
                var available = await CommittedLocationStock.ReadAsync(_store, _warehouse.Code, source.Item.InternalSKU, line.FromLocation.Trim(), ct);
                if (available < line.ReturnQty)
                    problems.Add(new("InsufficientStock", $"Only {available:0.####} of {source.Item.InternalSKU} is at {line.FromLocation.Trim()}."));
            }
        }

        var priorNet = await _db.SupplierCreditDocuments.AsNoTracking()
            .Where(c => c.OriginalShipmentId == original.Id && c.PostingStatus == "Posted" && c.Id != credit.Id)
            .SumAsync(c => (decimal?)c.NetAmount, ct) ?? 0m;
        if (original.NetAmount is not null && priorNet + credit.NetAmount > original.NetAmount.Value)
            problems.Add(new("CreditExceedsOriginal", "The credits for this document would exceed its net amount."));

        var normalized = PurchaseMath.NormalizeInvoiceNumber(credit.CreditNumber);
        var year = credit.CreditDate.Year;
        var sameCredits = await _db.SupplierCreditDocuments.AsNoTracking()
            .Where(x => x.SupplierId == credit.SupplierId && x.Id != credit.Id && x.PostingStatus == "Posted" && x.CreditDate.Year == year)
            .Select(x => x.CreditNumber).ToListAsync(ct);
        var sameInvoices = await _db.InboundShipments.AsNoTracking()
            .Where(x => x.SupplierId == credit.SupplierId && x.PostingStatus == "Posted" && x.InvoiceDate != null && x.InvoiceDate.Value.Year == year && x.InvoiceNumber != null)
            .Select(x => x.InvoiceNumber!).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(credit.CreditNumber)
            && sameCredits.Concat(sameInvoices).Any(n => PurchaseMath.NormalizeInvoiceNumber(n) == normalized))
            problems.Add(new("InvoiceNumberTaken", "This number is already used for this supplier in this year."));

        var rate = await ResolveRateAsync(credit.Currency, credit.CreditDate, null, null, ct);
        if (rate.Status == FxRateStatus.NotPublishedToday) problems.Add(new("RateNotPublishedToday", "The rate for today is not published yet."));
        else if (rate.UnitsPerEur is null) problems.Add(new("RateMissing", $"There is no exchange rate for {credit.Currency} on the document date."));

        var profiles = await ResolveItemProfilesAsync(credit.Lines.Select(l => l.ItemId).Distinct().ToList(), ct);
        var distinct = profiles.Values.Distinct().ToList();
        if (distinct.Count > 1)
            problems.Add(new("MixedStockAccounts", "The items belong to different stock accounts. Split the document."));
        else if (distinct.Count == 1 && credit.AccountingRoute == "Auto")
        {
            var treatment = credit.VatTreatment ?? PurchasePostingValidator.EffectiveVatTreatment(original, supplier);
            var rules = (await _db.AgnumPostingRules.AsNoTracking().Select(r => new { r.StockAccountProfileId, r.VatTreatment }).ToListAsync(ct))
                .Select(r => (r.StockAccountProfileId, r.VatTreatment)).ToHashSet();
            if (treatment is null) problems.Add(new("VatTreatmentRequired", "VAT treatment is required."));
            else if (distinct[0] is null || !rules.Contains((distinct[0]!.Value, treatment.Value)))
                problems.Add(new("PostingRuleMissing", $"No Agnum posting rule for this stock account and {treatment}."));
        }

        return problems;
    }
}
