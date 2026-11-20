using System.Globalization;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore.Storage;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record PostResult(bool NotFound, IReadOnlyList<PostingProblem> Problems)
{
    public bool Posted => !NotFound && Problems.Count == 0;
}

/// <summary>Blueprint 4.2 - 4.4: rate resolution, posting checks, posting of purchase and cost documents.</summary>
public sealed partial class PurchaseDocumentService : IPostingReverser
{
    private readonly WarehouseDbContext _db;
    private readonly IFxRateProvider _fx;
    private readonly IDocumentStore _store;
    private readonly IBalanceGuardLockFactory _locks;
    private readonly IWarehouseCodeProvider _warehouse;
    private readonly AgnumOutboxService _outbox;
    private readonly ILogger<PurchaseDocumentService> _logger;

    public PurchaseDocumentService(
        WarehouseDbContext db, IFxRateProvider fx, IDocumentStore store, IBalanceGuardLockFactory locks,
        IWarehouseCodeProvider warehouse, AgnumOutboxService outbox, ILogger<PurchaseDocumentService> logger)
    {
        _outbox = outbox;
        _db = db;
        _fx = fx;
        _store = store;
        _locks = locks;
        _warehouse = warehouse;
        _logger = logger;
    }

    public static DateOnly Today() => DateOnly.FromDateTime(StockBusinessDate.From(DateTime.UtcNow));

    public async Task<PostingRate> ResolveRateAsync(string currency, DateOnly date, decimal? manualRate, string? manualReason, CancellationToken ct)
    {
        if (manualRate is > 0m && !string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            return new PostingRate(FxRateStatus.Ok, manualRate, date, "Manual", manualReason);
        }

        var result = await _fx.GetRateAsync(currency, date, ct);
        return result.Quote is null
            ? new PostingRate(result.Status, null, null, null, null)
            : new PostingRate(FxRateStatus.Ok, result.Quote.UnitsPerEur, result.Quote.RateDate,
                string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase) ? "EUR" : result.Quote.Source, null);
    }

    /// <summary>Preliminary rate and EUR amounts for the header, best effort; never blocks saving the document.</summary>
    public async Task RefreshPreliminaryAsync(InboundShipment shipment, CancellationToken ct)
    {
        if (shipment.PostingStatus == "Posted")
        {
            return;
        }

        try
        {
            var rate = await ResolveRateAsync(shipment.Currency, shipment.InvoiceDate ?? Today(), null, null, ct);
            if (rate.UnitsPerEur is null)
            {
                shipment.RateUnitsPerEur = null;
                shipment.RateDate = null;
                shipment.RateSource = null;
                return;
            }

            shipment.RateUnitsPerEur = rate.UnitsPerEur;
            shipment.RateDate = rate.RateDate;
            shipment.RateSource = rate.Source;
            var eurPerUnit = PurchaseMath.RateEurPerUnit(rate.UnitsPerEur.Value);
            shipment.GrossAmount = shipment.NetAmount is null ? null : shipment.NetAmount + (shipment.VatAmount ?? 0m);
            shipment.NetEur = shipment.NetAmount is null ? null : PurchaseMath.ToEur(shipment.NetAmount.Value, eurPerUnit);
            shipment.VatEur = shipment.VatAmount is null ? null : PurchaseMath.ToEur(shipment.VatAmount.Value, eurPerUnit);
            shipment.GrossEur = shipment.NetEur is null ? null : shipment.NetEur + (shipment.VatEur ?? 0m);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preliminary rate for shipment {Shipment} could not be resolved.", shipment.Id);
        }
    }

    public async Task<PurchasePostingInput> BuildInputAsync(
        InboundShipment shipment, decimal? manualRate, string? manualReason, CancellationToken ct)
    {
        var supplier = shipment.Supplier ?? await _db.Suppliers.AsNoTracking().FirstAsync(x => x.Id == shipment.SupplierId, ct);
        var costTypes = await _db.AdditionalCostTypes.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var profiles = await ResolveItemProfilesAsync(shipment.Lines.Select(l => l.ItemId).Distinct().ToList(), ct);
        var rules = (await _db.AgnumPostingRules.AsNoTracking().Select(r => new { r.StockAccountProfileId, r.VatTreatment }).ToListAsync(ct))
            .Select(r => (r.StockAccountProfileId, r.VatTreatment)).ToHashSet();

        var date = shipment.InvoiceDate ?? Today();
        var rate = await ResolveRateAsync(shipment.Currency, date, manualRate, manualReason, ct);
        decimal? previous = rate.UnitsPerEur is null || shipment.Currency == "EUR"
            ? null
            : await _fx.GetPreviousRateAsync(shipment.Currency, rate.RateDate ?? date, ct);

        var taken = false;
        if (!string.IsNullOrWhiteSpace(shipment.InvoiceNumber) && shipment.InvoiceDate is not null)
        {
            var normalized = PurchaseMath.NormalizeInvoiceNumber(shipment.InvoiceNumber);
            var year = shipment.InvoiceDate.Value.Year;
            var others = await _db.InboundShipments.AsNoTracking()
                .Where(x => x.SupplierId == shipment.SupplierId && x.Id != shipment.Id && x.PostingStatus == "Posted"
                            && x.InvoiceDate != null && x.InvoiceDate.Value.Year == year && x.InvoiceNumber != null)
                .Select(x => x.InvoiceNumber!).ToListAsync(ct);
            taken = others.Any(n => PurchaseMath.NormalizeInvoiceNumber(n) == normalized);
            if (!taken)
            {
                var costDocs = await _db.AdditionalCostDocuments.AsNoTracking()
                    .Where(x => x.SupplierId == shipment.SupplierId && x.PostingStatus == "Posted" && x.DocumentDate.Year == year)
                    .Select(x => x.DocumentNumber).ToListAsync(ct);
                taken = costDocs.Any(n => PurchaseMath.NormalizeInvoiceNumber(n) == normalized);
            }
        }

        return new PurchasePostingInput
        {
            Shipment = shipment,
            Supplier = supplier,
            CostTypes = costTypes,
            ItemProfiles = profiles,
            HasPostingRule = (p, t) => rules.Contains((p, t)),
            Rate = rate,
            PreviousRateUnitsPerEur = previous,
            InvoiceNumberTaken = taken
        };
    }

    private async Task<Dictionary<int, int?>> ResolveItemProfilesAsync(IReadOnlyList<int> itemIds, CancellationToken ct)
    {
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.CategoryId }).ToListAsync(ct);
        var categories = (await _db.ItemCategories.AsNoTracking()
                .Select(c => new { c.Id, c.ParentCategoryId, c.StockAccountProfileId }).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => (c.ParentCategoryId, c.StockAccountProfileId));
        var defaultProfile = await _db.StockAccountProfiles.AsNoTracking().Where(p => p.IsDefault)
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return items.ToDictionary(i => i.Id, i => AccountingConfigService.ResolveProfileId(i.CategoryId, categories, defaultProfile));
    }

    private Task<InboundShipment?> LoadAsync(int id, CancellationToken ct)
        => _db.InboundShipments
            .Include(x => x.Supplier)
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.AdditionalCosts)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>Checks without posting; shown on the shipment screen next to the Post button.</summary>
    public async Task<IReadOnlyList<PostingProblem>> DryRunAsync(int id, CancellationToken ct)
    {
        var shipment = await LoadAsync(id, ct);
        if (shipment is null || shipment.PostingStatus == "Posted")
        {
            return [];
        }

        return PurchasePostingValidator.Validate(await BuildInputAsync(shipment, null, null, ct));
    }

    public async Task<PostResult> PostAsync(int id, decimal? manualRate, string? manualReason, string user, CancellationToken ct)
    {
        await using var guard = await _locks.CreateAsync(ct);
        await guard.AcquireAsync([StockLockKey.ForLocation(_warehouse.Code, "INBOUND", id.ToString(CultureInfo.InvariantCulture))], ct);

        var shipment = await LoadAsync(id, ct);
        if (shipment is null)
        {
            return new PostResult(true, []);
        }

        var input = await BuildInputAsync(shipment, manualRate, manualReason, ct);
        var problems = PurchasePostingValidator.Validate(input);
        if (problems.Count > 0)
        {
            return new PostResult(false, problems);
        }

        var profileId = input.ItemProfiles.Values.FirstOrDefault();
        var snapshot = PostingSnapshot.Capture(shipment);
        var requests = Apply(shipment, input, user, profileId);
        snapshot.Values = requests.Select(r => new SnapshotValue(r.Sku, r.Qty, r.ValueEur ?? 0m)).ToList();
        shipment.PostingSnapshotJson = snapshot.ToJson();
        shipment.AgnumKey ??= Guid.NewGuid();
        if (shipment.AccountingRoute == "Auto")
        {
            await QueuePostingAsync(shipment, input, profileId!.Value, user, ct);
        }

        await PersistAsync(requests, ct);
        return new PostResult(false, []);
    }

    /// <summary>Applies blueprint 4.2 "on posting" to the tracked entities and returns the value requests to write.</summary>
    public static List<StockValueRequest> Apply(InboundShipment shipment, PurchasePostingInput input, string user, int? profileId)
    {
        var rate = input.Rate;
        var lines = PurchasePostingValidator.LineInputs(shipment);
        var costs = PurchasePostingValidator.CostInputs(shipment, input.CostTypes);
        var totals = PurchaseMath.Compute(shipment.NetAmount!.Value, shipment.VatAmount!.Value, rate.UnitsPerEur!.Value, lines, costs);

        shipment.RateUnitsPerEur = rate.UnitsPerEur;
        shipment.RateDate = rate.RateDate;
        shipment.RateSource = rate.Source;
        shipment.RateReason = rate.ManualReason;
        shipment.GrossAmount = shipment.NetAmount + shipment.VatAmount;
        shipment.NetEur = totals.NetEur;
        shipment.VatEur = totals.VatEur;
        shipment.GrossEur = totals.GrossEur;
        shipment.VatTreatment = PurchasePostingValidator.EffectiveVatTreatment(shipment, input.Supplier);
        foreach (var cost in shipment.AdditionalCosts)
        {
            cost.Currency = shipment.Currency;
            cost.AmountEur = totals.CostEur[cost.Id];
        }

        var document = new StockValueDocument(
            "InboundShipment",
            shipment.Id.ToString(CultureInfo.InvariantCulture),
            shipment.ReferenceNumber,
            $"Posting of invoice {shipment.InvoiceNumber}",
            profileId);
        var requests = new List<StockValueRequest>();
        foreach (var line in shipment.Lines.OrderBy(l => l.Id))
        {
            line.InvoiceQty ??= line.ExpectedQty;
            line.InvoiceUnitPrice ??= line.UnitPrice;
            if (!totals.LineFinalEur.TryGetValue(line.Id, out var final))
            {
                continue;
            }

            line.FinalValueEur = final;
            var received = PurchaseMath.ReceivedPortion(final, line.ReceivedQty, line.InvoiceQty.Value);
            var diff = received - line.BookedValueEur;
            line.BookedValueEur = received;
            if (line.Item is not null && line.ReceivedQty > 0m)
            {
                requests.Add(StockValueRequest.ForTrueUp(line.Item.InternalSKU, line.ReceivedQty, diff, document));
            }
        }

        shipment.PostingStatus = "Posted";
        shipment.PostedAt = DateTimeOffset.UtcNow;
        shipment.PostedBy = user;
        shipment.PostingRevision++;
        return requests;
    }

    /// <summary>Blueprint 4.7: the outbox row is added to the context here and committed with the posting transaction.</summary>
    private async Task QueuePostingAsync(InboundShipment shipment, PurchasePostingInput input, int profileId, string user, CancellationToken ct)
    {
        var profile = await _db.StockAccountProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId, ct);
        var rule = await _db.AgnumPostingRules.AsNoTracking()
            .FirstAsync(r => r.StockAccountProfileId == profileId && r.VatTreatment == shipment.VatTreatment!.Value, ct);
        var expenses = shipment.AdditionalCosts
            .Where(c => input.CostTypes.TryGetValue(c.CostType, out var t) && !t.IncludedInCost && !string.IsNullOrWhiteSpace(t.ExpenseAccount))
            .Select(c => (input.CostTypes[c.CostType].ExpenseAccount!, c.Amount)).ToList();
        var payload = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            shipment.AgnumKey!.Value, shipment.PostingRevision, shipment.InvoiceNumber!, shipment.InvoiceDate!.Value,
            input.Supplier.AgnumClientId!.Value, profile.AgnumSndId, rule.SchemeId, shipment.Currency, shipment.RateUnitsPerEur!.Value,
            shipment.NetAmount!.Value, shipment.VatAmount!.Value, rule.VatCode, profile.StockAccount, expenses,
            Intrastat: IntrastatHeader(shipment)));
        _db.AgnumOutbox.Add(NewRow(payload, OutboxDocumentType.Shipment, shipment.Id, shipment.SupplierId, shipment.InvoiceNumber!,
            shipment.InvoiceDate!.Value, shipment.Currency, shipment.GrossAmount ?? 0m, user));
    }

    /// <summary>Intrastat header for the posting request; only an EU acquisition carries it.</summary>
    public static AgnumIntrastat? IntrastatHeader(InboundShipment shipment)
    {
        if (shipment.VatTreatment != VatTreatment.EuAcquisition) return null;
        string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        var header = new AgnumIntrastat
        {
            Nature = Clean(shipment.TransactionNature), DeliveryTerms = Clean(shipment.DeliveryTerms),
            Transport = Clean(shipment.TransportMode), Country = Clean(shipment.DispatchCountry)
        };
        return header.Nature is null && header.DeliveryTerms is null && header.Transport is null && header.Country is null ? null : header;
    }

    private static AgnumDocumentOutbox NewRow(BuiltPayload payload, string type, int id, int supplierId, string number,
        DateOnly date, string currency, decimal gross, string user) => new()
    {
        Key = Guid.Parse(payload.Request.Key), Revision = payload.Request.Revision, DocumentType = type, DocumentId = id,
        SupplierId = supplierId, DocumentNumber = number, DocumentDate = date, Currency = currency, GrossAmount = gross,
        Operation = OutboxOperation.Post, PayloadJson = payload.Json, PayloadHash = payload.Hash, Status = OutboxStatus.Pending,
        NextAttemptAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = user
    };

    private async Task PersistAsync(IReadOnlyList<StockValueRequest> requests, CancellationToken ct)
    {
        IReadOnlyList<StockValueChanged> written = [];
        await StockValueRetry.RunAsync(() => _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await using var session = _store.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (NpgsqlTransaction)transaction.GetDbTransaction(), false));
            await _db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            written = await StockValueWriter.AppendAsync(session, _db, requests, ct);
            await session.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _db.ChangeTracker.AcceptAllChanges();
        }), ct);
        await StockValueWriter.ProjectAsync(_db, _logger, written, ct);
    }

    // ---- Blueprint 4.4: a cost invoice of another company ----

    public async Task<List<PostingProblem>> ValidateCostDocumentAsync(AdditionalCostDocument doc, InboundShipment? basis, CancellationToken ct)
    {
        var problems = new List<PostingProblem>();
        if (doc.PostingStatus == "Posted") { problems.Add(new("AlreadyPosted", "The document is already posted.")); return problems; }
        if (string.IsNullOrWhiteSpace(doc.DocumentNumber)) problems.Add(new("InvoiceNumberRequired", "Document number is required."));
        if (doc.NetAmount <= 0m) problems.Add(new("AmountsRequired", "Net amount must be greater than zero."));
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == doc.SupplierId, ct);
        if (supplier is null) { problems.Add(new("SupplierNotPostable", "Supplier not found.")); return problems; }
        if (doc.AccountingRoute == "Auto")
        {
            var check = AgnumSupplierSyncService.CheckPostable(supplier);
            if (!check.Allowed) problems.Add(new("SupplierNotPostable", check.Reason ?? "Supplier cannot be posted."));
        }

        var type = await _db.AdditionalCostTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == doc.CostTypeCode && t.IsActive, ct);
        if (type is null) problems.Add(new("CostTypeInvalid", $"Additional cost type '{doc.CostTypeCode}' is not available."));
        if (basis is null || basis.PostingStatus != "Posted") problems.Add(new("BasisNotPosted", "The basis shipment must be posted first."));
        if (doc.AccountingRoute == "ManualInAgnum" && string.IsNullOrWhiteSpace(doc.ManualReason))
            problems.Add(new("ManualReasonRequired", "A reason is required when the document is entered in Agnum manually."));
        if (doc.AccountingRoute == "Auto" && basis is not null && basis.PostingStatus == "Posted")
        {
            var basisProfile = (await ResolveItemProfilesAsync(
                basis.Lines.Where(l => l.Item?.ItemType != ItemType.Service).Select(l => l.ItemId).Distinct().ToList(), ct)).Values.FirstOrDefault();
            if (basisProfile is null || !await _db.AgnumPostingRules.AnyAsync(r => r.StockAccountProfileId == basisProfile && r.VatTreatment == doc.VatTreatment, ct))
                problems.Add(new("PostingRuleMissing", $"No Agnum posting rule for the stock account of the basis shipment and {doc.VatTreatment}."));
            if (type is { IncludedInCost: false, ExpenseAccount: null or "" })
                problems.Add(new("CostTypeInvalid", $"Additional cost type '{doc.CostTypeCode}' has no expense account."));
        }


        var normalized = PurchaseMath.NormalizeInvoiceNumber(doc.DocumentNumber);
        var year = doc.DocumentDate.Year;
        var sameDocs = await _db.AdditionalCostDocuments.AsNoTracking()
            .Where(x => x.SupplierId == doc.SupplierId && x.Id != doc.Id && x.PostingStatus == "Posted" && x.DocumentDate.Year == year)
            .Select(x => x.DocumentNumber).ToListAsync(ct);
        var sameShipments = await _db.InboundShipments.AsNoTracking()
            .Where(x => x.SupplierId == doc.SupplierId && x.PostingStatus == "Posted" && x.InvoiceDate != null && x.InvoiceDate.Value.Year == year && x.InvoiceNumber != null)
            .Select(x => x.InvoiceNumber!).ToListAsync(ct);
        if (sameDocs.Concat(sameShipments).Any(n => PurchaseMath.NormalizeInvoiceNumber(n) == normalized))
            problems.Add(new("InvoiceNumberTaken", "This invoice number is already used for this supplier in this year."));

        var rate = await ResolveRateAsync(doc.Currency, doc.DocumentDate, null, null, ct);
        if (rate.Status == FxRateStatus.NotPublishedToday) problems.Add(new("RateNotPublishedToday", "The rate for today is not published yet."));
        else if (rate.UnitsPerEur is null) problems.Add(new("RateMissing", $"There is no exchange rate for {doc.Currency} on the document date."));
        return problems;
    }

    public async Task<PostResult> PostCostDocumentAsync(int id, string user, CancellationToken ct)
    {
        var doc = await _db.AdditionalCostDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return new PostResult(true, []);

        await using var guard = await _locks.CreateAsync(ct);
        await guard.AcquireAsync([StockLockKey.ForLocation(_warehouse.Code, "INBOUND", doc.BasisShipmentId.ToString(CultureInfo.InvariantCulture))], ct);
        var basis = await LoadAsync(doc.BasisShipmentId, ct);
        var problems = await ValidateCostDocumentAsync(doc, basis, ct);
        if (problems.Count > 0) return new PostResult(false, problems);

        var rate = await ResolveRateAsync(doc.Currency, doc.DocumentDate, null, null, ct);
        var eurPerUnit = PurchaseMath.RateEurPerUnit(rate.UnitsPerEur!.Value);
        doc.RateUnitsPerEur = rate.UnitsPerEur;
        doc.RateDate = rate.RateDate;
        doc.RateSource = rate.Source;
        doc.NetEur = PurchaseMath.ToEur(doc.NetAmount, eurPerUnit);
        doc.GrossAmount = doc.NetAmount + doc.VatAmount;

        var requests = new List<StockValueRequest>();
        var type = await _db.AdditionalCostTypes.AsNoTracking().FirstAsync(t => t.Code == doc.CostTypeCode, ct);
        var snapshot = PostingSnapshot.Capture(doc, basis!.Lines);
        if (type.IncludedInCost && basis is not null)
        {
            var stock = basis.Lines.Where(l => l.Item?.ItemType != ItemType.Service && l.FinalValueEur is not null).OrderBy(l => l.Id).ToList();
            var shares = PurchaseMath.Allocate(doc.NetEur.Value, stock.Select(l => l.FinalValueEur!.Value).ToList());
            var profile = (await ResolveItemProfilesAsync(stock.Select(l => l.ItemId).ToList(), ct)).Values.FirstOrDefault();
            var document = new StockValueDocument("AdditionalCostDocument",
                doc.Id.ToString(CultureInfo.InvariantCulture), doc.DocumentNumber, $"Additional cost {doc.CostTypeCode} for {basis.ReferenceNumber}", profile);
            for (var i = 0; i < stock.Count; i++)
            {
                var line = stock[i];
                line.FinalValueEur += shares[i];
                var received = PurchaseMath.ReceivedPortion(line.FinalValueEur!.Value, line.ReceivedQty, line.InvoiceQty ?? line.ExpectedQty);
                var diff = received - line.BookedValueEur;
                line.BookedValueEur = received;
                if (line.ReceivedQty > 0m && line.Item is not null)
                    requests.Add(StockValueRequest.ForTrueUp(line.Item.InternalSKU, line.ReceivedQty, diff, document));
            }
        }

        doc.PostingStatus = "Posted";
        doc.PostedAt = DateTimeOffset.UtcNow;
        doc.PostedBy = user;
        doc.PostingRevision++;
        doc.AgnumKey ??= Guid.NewGuid();
        snapshot.Values = requests.Select(r => new SnapshotValue(r.Sku, r.Qty, r.ValueEur ?? 0m)).ToList();
        doc.PostingSnapshotJson = snapshot.ToJson();
        if (doc.AccountingRoute == "Auto")
        {
            await QueueCostDocumentAsync(doc, basis!, type, user, ct);
        }

        await PersistAsync(requests, ct);
        return new PostResult(false, []);
    }

    private async Task QueueCostDocumentAsync(AdditionalCostDocument doc, InboundShipment basis, AdditionalCostType type, string user, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == doc.SupplierId, ct);
        var profileId = (await ResolveItemProfilesAsync(
            basis.Lines.Where(l => l.Item?.ItemType != ItemType.Service).Select(l => l.ItemId).Distinct().ToList(), ct)).Values.First()!.Value;
        var profile = await _db.StockAccountProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId, ct);
        var rule = await _db.AgnumPostingRules.AsNoTracking()
            .FirstAsync(r => r.StockAccountProfileId == profileId && r.VatTreatment == doc.VatTreatment, ct);
        IReadOnlyList<(string, decimal)> expenses = type.IncludedInCost ? [] : [(type.ExpenseAccount!, doc.NetAmount)];
        var payload = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            doc.AgnumKey!.Value, doc.PostingRevision, doc.DocumentNumber, doc.DocumentDate, supplier.AgnumClientId!.Value,
            profile.AgnumSndId, rule.SchemeId, doc.Currency, doc.RateUnitsPerEur!.Value, doc.NetAmount, doc.VatAmount, rule.VatCode,
            profile.StockAccount, expenses));
        _db.AgnumOutbox.Add(NewRow(payload, OutboxDocumentType.CostDocument, doc.Id, doc.SupplierId, doc.DocumentNumber,
            doc.DocumentDate, doc.Currency, doc.GrossAmount, user));
    }

    // ---- Blueprint 4.8: un-post ----

    public sealed record UnpostResult(bool NotFound, string Outcome, IReadOnlyList<PostingProblem> Problems)
    {
        public const string Reversed = "Reversed";
        public const string Queued = "AnnulQueued";
        public const string AlreadyQueued = "AlreadyQueued";
        public const string Blocked = "Blocked";
    }

    /// <summary>
    /// Un-post request of the accountant. Unsent and manual documents are reversed at once; a document that may be
    /// in Agnum gets an Annul row and is reversed by the sender after the service deleted it.
    /// </summary>
    public async Task<UnpostResult> UnpostAsync(string documentType, int id, string user, CancellationToken ct)
    {
        var info = await LoadUnpostInfoAsync(documentType, id, ct);
        if (info is null) return new UnpostResult(true, UnpostResult.Blocked, []);
        var problems = await CheckReversibleAsync(documentType, id, ct);
        if (problems.Count > 0) return new UnpostResult(false, UnpostResult.Blocked, problems);

        var plan = await _outbox.PlanUnpostAsync(documentType, id, info.SupplierId, info.Route, info.Key, info.Revision,
            info.Number, info.Date, info.Currency, info.Gross, user, ct);
        switch (plan)
        {
            case UnpostPlan.ReverseNow:
                await ReverseAsync(documentType, id, user, ct);
                return new UnpostResult(false, UnpostResult.Reversed, []);
            case UnpostPlan.AnnulQueued:
                return new UnpostResult(false, UnpostResult.Queued, []);
            default:
                return new UnpostResult(false, UnpostResult.AlreadyQueued, []);
        }
    }

    private sealed record UnpostInfo(int SupplierId, string Route, Guid? Key, int Revision, string? Number, DateOnly Date, string Currency, decimal Gross, bool Posted);

    private async Task<UnpostInfo?> LoadUnpostInfoAsync(string documentType, int id, CancellationToken ct)
    {
        if (documentType == OutboxDocumentType.CostDocument)
        {
            var d = await _db.AdditionalCostDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return d is null ? null : new(d.SupplierId, d.AccountingRoute, d.AgnumKey, d.PostingRevision, d.DocumentNumber, d.DocumentDate, d.Currency, d.GrossAmount, d.PostingStatus == "Posted");
        }

        var s = await _db.InboundShipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new(s.SupplierId, s.AccountingRoute, s.AgnumKey, s.PostingRevision, s.InvoiceNumber, s.InvoiceDate ?? Today(), s.Currency, s.GrossAmount ?? 0m, s.PostingStatus == "Posted");
    }

    /// <summary>Checks that must hold before a posting can be taken back.</summary>
    public async Task<List<PostingProblem>> CheckReversibleAsync(string documentType, int id, CancellationToken ct)
    {
        var problems = new List<PostingProblem>();
        if (documentType == OutboxDocumentType.CostDocument)
        {
            var d = await _db.AdditionalCostDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null || d.PostingStatus != "Posted") { problems.Add(new("NotPosted", "The document is not posted.")); return problems; }
            var snapshot = PostingSnapshot.FromJson(d.PostingSnapshotJson);
            if (snapshot is null) { problems.Add(new("UnpostNotAvailable", "The document was posted before un-posting existed; no values were saved.")); return problems; }
            var basisLines = await _db.InboundShipmentLines.AsNoTracking().Where(l => l.ShipmentId == d.BasisShipmentId).ToListAsync(ct);
            if (snapshot.ReceiptChangesSincePosting(basisLines).Count > 0)
                problems.Add(new("ReceiptsAfterPosting", "Goods were received after posting; un-posting would misstate the stock value."));
            return problems;
        }

        var s = await _db.InboundShipments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null || s.PostingStatus != "Posted") { problems.Add(new("NotPosted", "The document is not posted.")); return problems; }
        var snap = PostingSnapshot.FromJson(s.PostingSnapshotJson);
        if (snap is null) { problems.Add(new("UnpostNotAvailable", "The document was posted before un-posting existed; no values were saved.")); return problems; }
        if (await _db.AdditionalCostDocuments.AnyAsync(x => x.BasisShipmentId == id && x.PostingStatus == "Posted", ct))
            problems.Add(new("CostDocumentsPosted", "Un-post the additional cost documents of this shipment first."));
        if (snap.ReceiptChangesSincePosting(s.Lines).Count > 0)
            problems.Add(new("ReceiptsAfterPosting", "Goods were received after posting; un-posting would misstate the stock value."));
        return problems;
    }

    /// <summary>
    /// Takes a posting back: NotPosted, header and line values as before posting, stock value reversed by opposite
    /// true-ups, outbox rows of this revision withdrawn. Idempotent: a document that is not posted is left alone.
    /// </summary>
    public async Task ReverseAsync(string documentType, int documentId, string user, CancellationToken ct)
    {
        var basisId = documentId;
        if (documentType == OutboxDocumentType.CostDocument)
        {
            basisId = await _db.AdditionalCostDocuments.AsNoTracking().Where(d => d.Id == documentId)
                .Select(d => d.BasisShipmentId).FirstOrDefaultAsync(ct);
            if (basisId == 0) return;
        }

        await using var guard = await _locks.CreateAsync(ct);
        await guard.AcquireAsync([StockLockKey.ForLocation(_warehouse.Code, "INBOUND", basisId.ToString(CultureInfo.InvariantCulture))], ct);

        Guid? key;
        int revision;
        PostingSnapshot snapshot;
        string number;
        if (documentType == OutboxDocumentType.CostDocument)
        {
            var doc = await _db.AdditionalCostDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);
            if (doc is null || doc.PostingStatus != "Posted") return;
            var problems = await CheckReversibleAsync(documentType, documentId, ct);
            if (problems.Count > 0) throw new InvalidOperationException(problems[0].Text);
            snapshot = PostingSnapshot.FromJson(doc.PostingSnapshotJson)!;
            var basis = await LoadAsync(doc.BasisShipmentId, ct);
            key = doc.AgnumKey; revision = doc.PostingRevision; number = doc.DocumentNumber;
            snapshot.Restore(doc, basis!.Lines);
        }
        else
        {
            var shipment = await LoadAsync(documentId, ct);
            if (shipment is null || shipment.PostingStatus != "Posted") return;
            var problems = await CheckReversibleAsync(documentType, documentId, ct);
            if (problems.Count > 0) throw new InvalidOperationException(problems[0].Text);
            snapshot = PostingSnapshot.FromJson(shipment.PostingSnapshotJson)!;
            key = shipment.AgnumKey; revision = shipment.PostingRevision; number = shipment.ReferenceNumber;
            snapshot.Restore(shipment);
        }

        var reason = new StockValueDocument(documentType, documentId.ToString(CultureInfo.InvariantCulture), number, $"Un-post by {user}");
        var requests = snapshot.Values.Select(v => StockValueRequest.ForTrueUp(v.Sku, v.Qty, -v.DiffEur, reason)).ToList();
        if (key is not null) await _outbox.WithdrawPostRowsAsync(key.Value, revision, ct);
        await PersistAsync(requests, ct);
    }
}
