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
public sealed class PurchaseDocumentService
{
    private readonly WarehouseDbContext _db;
    private readonly IFxRateProvider _fx;
    private readonly IDocumentStore _store;
    private readonly IBalanceGuardLockFactory _locks;
    private readonly IWarehouseCodeProvider _warehouse;
    private readonly ILogger<PurchaseDocumentService> _logger;

    public PurchaseDocumentService(
        WarehouseDbContext db, IFxRateProvider fx, IDocumentStore store, IBalanceGuardLockFactory locks,
        IWarehouseCodeProvider warehouse, ILogger<PurchaseDocumentService> logger)
    {
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
        var requests = Apply(shipment, input, user, profileId);
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
        await PersistAsync(requests, ct);
        return new PostResult(false, []);
    }
}
