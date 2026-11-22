using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

/// <summary>A blocking message. <see cref="Code"/> maps to a localized text in the UI; <see cref="Text"/> is the English fallback.
/// <see cref="Detail"/> lists what the message is about (for example the offending items) and is language neutral.</summary>
public sealed record PostingProblem(string Code, string Text, string? Detail = null);

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

    /// <summary>Most items named in one blocking message; the rest is summarised as "and N more".</summary>
    public const int MaxListedItems = 5;

    /// <summary>"SKU Name" of each distinct line item, at most <see cref="MaxListedItems"/>, then the overflow
    /// formatted by <paramref name="more"/> (receives the number of items left out).</summary>
    public static string? DescribeItems(IEnumerable<InboundShipmentLine> lines, Func<int, string> more)
    {
        var distinct = lines.GroupBy(l => l.ItemId).Select(g => g.First()).ToList();
        if (distinct.Count == 0) return null;
        var text = string.Join(", ", distinct.Take(MaxListedItems).Select(DescribeItem));
        return distinct.Count > MaxListedItems ? $"{text} {more(distinct.Count - MaxListedItems)}" : text;
    }

    private static string DescribeItem(InboundShipmentLine line)
    {
        if (line.Item is null) return $"#{line.ItemId}";
        return string.IsNullOrWhiteSpace(line.Item.Name) ? line.Item.InternalSKU : $"{line.Item.InternalSKU} {line.Item.Name}";
    }

    private static PostingProblem WithItems(string code, string text, List<InboundShipmentLine> lines)
    {
        var english = DescribeItems(lines, n => $"and {n} more");
        // The detail is language neutral ("(+N)" for the overflow); the UI puts it behind the localized sentence.
        var neutral = DescribeItems(lines, n => $"(+{n})");
        return new(code, english is null ? text : $"{text.TrimEnd('.')}: {english}.", neutral);
    }

    private static void AddIntrastatItemProblems(List<InboundShipmentLine> stockLines, List<PostingProblem> problems)
    {
        var noGoodsData = stockLines.Where(l => l.Item is null || !IsCommodityCode(l.Item.CommodityCode) || l.Item.NetWeightKg is not > 0m).ToList();
        if (noGoodsData.Count > 0)
            problems.Add(WithItems("IntrastatItemDataMissing", "Every stock item of an EU acquisition needs an 8-digit commodity code and a net weight.", noGoodsData));

        var noOrigin = stockLines.Where(l => l.Item is not null && !IntrastatCodes.IsCountryCode(l.Item.OriginCountry)).ToList();
        if (noOrigin.Count > 0)
            problems.Add(WithItems("IntrastatOriginCountryMissing", "Every stock item of an EU acquisition needs a country of origin (ISO 3166 alpha-2 code).", noOrigin));

        var badSupplementary = stockLines.Where(l => l.Item is not null
            && !string.IsNullOrWhiteSpace(l.Item.SupplementaryUnit) != (l.Item.SupplementaryUnitFactor is > 0m)).ToList();
        if (badSupplementary.Count > 0)
            problems.Add(WithItems("IntrastatSupplementaryUnitInvalid", "A supplementary unit and its factor (above zero) must be set together on the item.", badSupplementary));
    }

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

        if (s.AccountingRoute == "ManualInAgnum" && string.IsNullOrWhiteSpace(s.ManualReason))
            problems.Add(new("ManualReasonRequired", "A reason is required when the document is entered in Agnum manually."));

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
            AddIntrastatItemProblems(stockLines, problems);

            var invalid = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.TransactionNature) && !IntrastatCodes.IsNature(s.TransactionNature)) invalid.Add("transaction nature");
            if (!string.IsNullOrWhiteSpace(s.TransportMode) && !IntrastatCodes.IsTransportMode(s.TransportMode)) invalid.Add("transport mode");
            if (!string.IsNullOrWhiteSpace(s.DeliveryTerms) && !IntrastatCodes.IsDeliveryTerms(s.DeliveryTerms)) invalid.Add("delivery terms");
            if (invalid.Count > 0)
                problems.Add(new("IntrastatCodeInvalid", $"Not a valid Intrastat code: {string.Join(", ", invalid)}."));
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
