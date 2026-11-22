using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

/// <summary>
/// One purchase (or return) line that enters the Intrastat arrivals report. <see cref="ValueEur"/> is the invoice value
/// (quantity x invoice unit price at the document rate, without allocated costs); <see cref="LandedValueEur"/> is the
/// landed cost, kept only as information.
/// </summary>
public sealed record IntrastatLineInput(
    string CommodityCode,
    string Country,
    string OriginCountry,
    string Nature,
    string DeliveryTerms,
    string TransportMode,
    string? SupplementaryUnit,
    decimal Qty,
    decimal? SupplementaryFactor,
    decimal ValueEur,
    decimal? NetWeightKg,
    decimal? LandedValueEur = null,
    bool IsReturn = false);

public sealed record IntrastatRow(
    string CommodityCode,
    string Country,
    string OriginCountry,
    string Nature,
    string DeliveryTerms,
    string TransportMode,
    string? SupplementaryUnit,
    bool IsReturn,
    decimal ValueEur,
    decimal NetMassKg,
    decimal Quantity,
    decimal? SupplementaryQuantity,
    decimal? LandedValueEur,
    int Lines);

public sealed record IntrastatCollected(IReadOnlyList<IntrastatLineInput> Lines, IReadOnlyList<string> Warnings);

/// <summary>Blueprint 4.9 Intrastat arrivals: grouped by the declared classifying fields. Pure functions.</summary>
public static class IntrastatReport
{
    /// <summary>
    /// Turns documents into report lines. Only posted EU acquisitions enter; the value is the invoice value.
    /// Posted supplier returns (credit lines that send goods back) enter as separate lines flagged as returns with
    /// transaction code 21. Lines with missing declaration data are left out and reported as "document|SKU" warnings.
    /// </summary>
    public static IntrastatCollected Collect(
        IEnumerable<InboundShipment> shipments,
        IEnumerable<SupplierCreditDocument> credits,
        IReadOnlyDictionary<int, InboundShipment> originals)
    {
        var lines = new List<IntrastatLineInput>();
        var warnings = new List<string>();

        foreach (var s in shipments)
        {
            if (s.DocumentKind != "Invoice" || s.PostingStatus != "Posted" || !IsEuAcquisition(s)) continue;
            var rate = RateEurPerUnit(s.Currency, s.RateUnitsPerEur);
            foreach (var l in s.Lines.Where(l => l.Item is { ItemType: ItemType.Stock }))
            {
                var qty = l.InvoiceQty ?? l.ExpectedQty;
                decimal? value = rate is null ? null : PurchaseMath.ToEur(qty * (l.InvoiceUnitPrice ?? l.UnitPrice ?? 0m), rate.Value);
                AddLine(lines, warnings, s, l.Item!, s.InvoiceNumber ?? s.ReferenceNumber, qty, value, l.FinalValueEur, false);
            }
        }

        foreach (var c in credits)
        {
            if (c.PostingStatus != "Posted" || !originals.TryGetValue(c.OriginalShipmentId, out var original)
                || !IsEuAcquisition(original))
                continue;
            var rate = RateEurPerUnit(c.Currency, c.RateUnitsPerEur);
            foreach (var l in c.Lines.Where(l => l.Item is { ItemType: ItemType.Stock }))
            {
                if (l.ReturnQty <= 0m)
                {
                    // A price-only credit changes the value, not the goods: it is a correction of the arrival, not a return.
                    warnings.Add($"{c.CreditNumber}|{l.Item!.InternalSKU}");
                    continue;
                }

                var value = l.NetEur ?? (rate is null ? (decimal?)null : PurchaseMath.ToEur(l.NetAmount, rate.Value));
                AddLine(lines, warnings, original, l.Item!, c.CreditNumber, l.ReturnQty, value, null, true);
            }
        }

        return new IntrastatCollected(lines, warnings);
    }

    private static bool IsEuAcquisition(InboundShipment s)
        => (s.VatTreatment ?? (s.Supplier?.Kind == SupplierKind.Eu ? VatTreatment.EuAcquisition : null)) == VatTreatment.EuAcquisition;

    private static decimal? RateEurPerUnit(string currency, decimal? unitsPerEur)
        => currency == "EUR" ? 1m
            : unitsPerEur is > 0m ? PurchaseMath.RateEurPerUnit(unitsPerEur.Value) : null;

    private static void AddLine(
        List<IntrastatLineInput> lines, List<string> warnings, InboundShipment header, Item item,
        string? docRef, decimal qty, decimal? value, decimal? landed, bool isReturn)
    {
        var nature = isReturn ? IntrastatCodes.ReturnNature : header.TransactionNature;
        var unit = string.IsNullOrWhiteSpace(item.SupplementaryUnit) ? null : item.SupplementaryUnit.Trim();
        if (string.IsNullOrWhiteSpace(item.CommodityCode)
            || string.IsNullOrWhiteSpace(header.DispatchCountry)
            || string.IsNullOrWhiteSpace(item.OriginCountry)
            || string.IsNullOrWhiteSpace(nature)
            || string.IsNullOrWhiteSpace(header.DeliveryTerms)
            || string.IsNullOrWhiteSpace(IntrastatCodes.NormalizeTransportMode(header.TransportMode))
            || (unit is not null && item.SupplementaryUnitFactor is not > 0m)
            || value is null)
        {
            warnings.Add($"{docRef}|{item.InternalSKU}");
            return;
        }

        lines.Add(new IntrastatLineInput(
            item.CommodityCode, header.DispatchCountry, item.OriginCountry, nature, header.DeliveryTerms,
            IntrastatCodes.NormalizeTransportMode(header.TransportMode)!, unit, qty, item.SupplementaryUnitFactor, value.Value, item.NetWeightKg, landed, isReturn));
    }

    /// <summary>A zero report is shown only when nothing is declared, nothing was skipped and there are no return rows.</summary>
    public static bool IsZeroReport(IReadOnlyCollection<IntrastatRow> rows, IReadOnlyCollection<string> warnings)
        => rows.Count == 0 && warnings.Count == 0;

    public static IReadOnlyList<IntrastatRow> Build(IEnumerable<IntrastatLineInput> lines)
        => lines
            .GroupBy(l => (
                Code: l.CommodityCode.Trim(),
                Country: l.Country.Trim().ToUpperInvariant(),
                Origin: l.OriginCountry.Trim().ToUpperInvariant(),
                Nature: l.Nature.Trim(),
                Terms: l.DeliveryTerms.Trim().ToUpperInvariant(),
                Mode: l.TransportMode.Trim(),
                Unit: string.IsNullOrWhiteSpace(l.SupplementaryUnit) ? null : l.SupplementaryUnit.Trim(),
                l.IsReturn))
            .Select(g => new IntrastatRow(
                g.Key.Code,
                g.Key.Country,
                g.Key.Origin,
                g.Key.Nature,
                g.Key.Terms,
                g.Key.Mode,
                g.Key.Unit,
                g.Key.IsReturn,
                PurchaseMath.Money(g.Sum(l => l.ValueEur)),
                decimal.Round(g.Sum(l => l.Qty * (l.NetWeightKg ?? 0m)), 3, MidpointRounding.AwayFromZero),
                g.Sum(l => l.Qty),
                g.Key.Unit is null ? null : decimal.Round(g.Sum(l => l.Qty * (l.SupplementaryFactor ?? 0m)), 3, MidpointRounding.AwayFromZero),
                g.Any(l => l.LandedValueEur is not null) ? PurchaseMath.Money(g.Sum(l => l.LandedValueEur ?? 0m)) : null,
                g.Count()))
            .OrderBy(r => r.IsReturn)
            .ThenBy(r => r.Country, StringComparer.Ordinal)
            .ThenBy(r => r.CommodityCode, StringComparer.Ordinal)
            .ThenBy(r => r.OriginCountry, StringComparer.Ordinal)
            .ThenBy(r => r.Nature, StringComparer.Ordinal)
            .ThenBy(r => r.DeliveryTerms, StringComparer.Ordinal)
            .ThenBy(r => r.TransportMode, StringComparer.Ordinal)
            .ThenBy(r => r.SupplementaryUnit ?? string.Empty, StringComparer.Ordinal)
            .ToList();

    public static string ToCsv(IEnumerable<IntrastatRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("CommodityCode;DispatchCountry;OriginCountry;NatureOfTransaction;DeliveryTerms;TransportMode;"
                  + "SupplementaryUnit;Return;NetMassKg;SupplementaryQuantity;Quantity;InvoiceValueEur;LandedCostEur\r\n");
        foreach (var r in rows)
        {
            sb.Append(r.CommodityCode).Append(';').Append(r.Country).Append(';').Append(r.OriginCountry).Append(';')
              .Append(r.Nature).Append(';').Append(r.DeliveryTerms).Append(';').Append(r.TransportMode).Append(';')
              .Append(r.SupplementaryUnit).Append(';').Append(r.IsReturn ? "1" : "0").Append(';')
              .Append(r.NetMassKg.ToString("0.000", inv)).Append(';')
              .Append(r.SupplementaryQuantity?.ToString("0.###", inv)).Append(';')
              .Append(r.Quantity.ToString("0.####", inv)).Append(';')
              .Append(r.ValueEur.ToString("0.00", inv)).Append(';')
              .Append(r.LandedValueEur?.ToString("0.00", inv)).Append("\r\n");
        }

        return sb.ToString();
    }
}
