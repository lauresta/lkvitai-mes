using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

/// <summary>A blocking message. <see cref="Code"/> maps to a localized text in the UI; <see cref="Text"/> is the English fallback.</summary>
public sealed record PostingProblem(string Code, string Text);

public sealed record PostingRate(FxRateStatus Status, decimal? UnitsPerEur, DateOnly? RateDate, string? Source, string? ManualReason);

public sealed class PurchasePostingInput
{
    public required InboundShipment Shipment { get; init; }
    public required Supplier Supplier { get; init; }
    public required IReadOnlyDictionary<string, AdditionalCostType> CostTypes { get; init; }
    /// <summary>Item id -> resolved stock account profile id (null: none configured) for stock items of the document.</summary>
    public required IReadOnlyDictionary<int, int?> ItemProfiles { get; init; }
    public required Func<int, VatTreatment, bool> HasPostingRule { get; init; }
    public required PostingRate Rate { get; init; }
    public decimal? PreviousRateUnitsPerEur { get; init; }
    public bool InvoiceNumberTaken { get; init; }
}

/// <summary>Blueprint 4.2 posting checks. All of them are blocking; a document is posted only when the list is empty.</summary>
public static class PurchasePostingValidator
{
    public const decimal MaxRateJump = 1.5m;

    public static VatTreatment? DefaultVatTreatment(SupplierKind? kind) => kind switch
    {
        SupplierKind.Domestic => VatTreatment.Domestic,
        SupplierKind.Eu => VatTreatment.EuAcquisition,
        SupplierKind.NonEu => VatTreatment.Import,
        _ => null
    };

    public static VatTreatment? EffectiveVatTreatment(InboundShipment shipment, Supplier supplier)
        => shipment.VatTreatment ?? DefaultVatTreatment(supplier.Kind);

    public static IReadOnlyList<PurchaseLineInput> LineInputs(InboundShipment shipment)
        => shipment.Lines.OrderBy(l => l.Id).Select(l => new PurchaseLineInput(
            l.Id,
            l.InvoiceQty ?? l.ExpectedQty,
            l.InvoiceUnitPrice ?? l.UnitPrice ?? 0m,
            l.Item?.ItemType == ItemType.Service)).ToList();

    public static IReadOnlyList<PurchaseCostInput> CostInputs(
        InboundShipment shipment, IReadOnlyDictionary<string, AdditionalCostType> types)
        => shipment.AdditionalCosts.OrderBy(c => c.Id).Select(c => new PurchaseCostInput(
            c.Id, c.Amount, !types.TryGetValue(c.CostType, out var t) || t.IncludedInCost)).ToList();

    public static bool IsCommodityCode(string? code)
        => code is { Length: 8 } && code.All(char.IsAsciiDigit);

    public static List<PostingProblem> Validate(PurchasePostingInput input)
    {
        var s = input.Shipment;
        var problems = new List<PostingProblem>();
        if (s.PostingStatus == "Posted")
        {
            problems.Add(new("AlreadyPosted", "The document is already posted."));
            return problems;
        }

        if (string.IsNullOrWhiteSpace(s.InvoiceNumber))
            problems.Add(new("InvoiceNumberRequired", "Supplier invoice number is required."));
        if (s.InvoiceDate is null)
            problems.Add(new("InvoiceDateRequired", "Supplier invoice date is required."));
        if (s.NetAmount is null || s.VatAmount is null)
            problems.Add(new("AmountsRequired", "Net and VAT amounts from the invoice are required."));

        if (s.AccountingRoute == "Auto")
        {
            var check = AgnumSupplierSyncService.CheckPostable(input.Supplier);
            if (!check.Allowed)
                problems.Add(new("SupplierNotPostable", check.Reason ?? "Supplier cannot be posted."));
        }

        var stockLines = s.Lines.Where(l => l.Item?.ItemType != ItemType.Service).ToList();
        if (stockLines.Count == 0)
            problems.Add(new("NoStockLines", "The document has no stock lines."));

        foreach (var cost in s.AdditionalCosts)
        {
            if (!input.CostTypes.TryGetValue(cost.CostType, out var type) || !type.IsActive)
                problems.Add(new("CostTypeInvalid", $"Additional cost type '{cost.CostType}' is not available."));
        }

        // Check 1: exact equality in the document currency.
        if (s.NetAmount is not null)
        {
            var total = PurchaseMath.LinesAndCostsTotal(LineInputs(s), CostInputs(s, input.CostTypes));
            if (total != s.NetAmount.Value)
                problems.Add(new("LinesTotalMismatch",
                    $"Lines and additional costs total {total:0.00####} {s.Currency}, but the net amount is {s.NetAmount.Value:0.00} {s.Currency}."));
        }

        if (input.InvoiceNumberTaken)
            problems.Add(new("InvoiceNumberTaken", "This invoice number is already used for this supplier in this year."));

        // Check 4: rate.
        var rate = input.Rate;
        if (rate.Status == FxRateStatus.NotPublishedToday)
            problems.Add(new("RateNotPublishedToday", "The rate for today is not published yet."));
        else if (rate.Status != FxRateStatus.Ok || rate.UnitsPerEur is null)
            problems.Add(new("RateMissing", $"There is no exchange rate for {s.Currency} on the document date."));
        else
        {
            if (rate.Source == "Manual" && string.IsNullOrWhiteSpace(rate.ManualReason))
                problems.Add(new("RateReasonRequired", "A manual rate needs a reason."));
            if (input.PreviousRateUnitsPerEur is > 0m)
            {
                var now = PurchaseMath.RateEurPerUnit(rate.UnitsPerEur.Value);
                var before = PurchaseMath.RateEurPerUnit(input.PreviousRateUnitsPerEur.Value);
                var ratio = now > before ? now / before : before / now;
                if (ratio > MaxRateJump)
                    problems.Add(new("RateSanity", $"The rate differs from the last saved rate more than {MaxRateJump}x."));
            }
        }

        // Blueprint 4.9: an EU acquisition needs the Intrastat header and the goods data.
        if (EffectiveVatTreatment(s, input.Supplier) == VatTreatment.EuAcquisition)
        {
            if (string.IsNullOrWhiteSpace(s.DeliveryTerms) || string.IsNullOrWhiteSpace(s.TransportMode)
                || string.IsNullOrWhiteSpace(s.DispatchCountry) || string.IsNullOrWhiteSpace(s.TransactionNature))
                problems.Add(new("IntrastatHeaderRequired", "Intrastat fields (delivery terms, transport mode, dispatch country, transaction nature) are required for an EU acquisition."));
            if (stockLines.Any(l => l.Item is null || !IsCommodityCode(l.Item.CommodityCode) || l.Item.NetWeightKg is not > 0m))
                problems.Add(new("IntrastatItemDataMissing", "Every stock item of an EU acquisition needs an 8-digit commodity code and a net weight."));
        }

        // Check 5 and 6: one stock account, a configured posting rule.
        var profiles = stockLines.Select(l => input.ItemProfiles.TryGetValue(l.ItemId, out var p) ? p : null).Distinct().ToList();
        if (profiles.Count > 1)
            problems.Add(new("MixedStockAccounts", "The items belong to different stock accounts. Split the shipment."));
        else if (profiles.Count == 1 && s.AccountingRoute == "Auto")
        {
            var treatment = EffectiveVatTreatment(s, input.Supplier);
            if (treatment is null)
                problems.Add(new("VatTreatmentRequired", "VAT treatment is required."));
            else if (profiles[0] is null || !input.HasPostingRule(profiles[0]!.Value, treatment.Value))
                problems.Add(new("PostingRuleMissing", $"No Agnum posting rule for this stock account and {treatment}."));
        }

        return problems;
    }
}
