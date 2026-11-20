using System.Globalization;
using System.Text;

namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

/// <summary>One purchase line that enters the Intrastat arrivals report.</summary>
public sealed record IntrastatLineInput(
    string CommodityCode, string Country, decimal Qty, decimal ValueEur, decimal? NetWeightKg);

public sealed record IntrastatRow(
    string CommodityCode, string Country, decimal ValueEur, decimal NetMassKg, decimal Quantity, int Lines);

/// <summary>Blueprint 4.9 Intrastat arrivals: grouped by commodity code and dispatch country. Pure functions.</summary>
public static class IntrastatReport
{
    public static IReadOnlyList<IntrastatRow> Build(IEnumerable<IntrastatLineInput> lines)
        => lines
            .GroupBy(l => (Code: l.CommodityCode.Trim(), Country: l.Country.Trim().ToUpperInvariant()))
            .Select(g => new IntrastatRow(
                g.Key.Code,
                g.Key.Country,
                PurchaseMath.Money(g.Sum(l => l.ValueEur)),
                decimal.Round(g.Sum(l => l.Qty * (l.NetWeightKg ?? 0m)), 3, MidpointRounding.AwayFromZero),
                g.Sum(l => l.Qty),
                g.Count()))
            .OrderBy(r => r.Country, StringComparer.Ordinal)
            .ThenBy(r => r.CommodityCode, StringComparer.Ordinal)
            .ToList();

    public static string ToCsv(IEnumerable<IntrastatRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("CommodityCode;DispatchCountry;ValueEur;NetMassKg;Quantity\r\n");
        foreach (var r in rows)
        {
            sb.Append(r.CommodityCode).Append(';').Append(r.Country).Append(';')
              .Append(r.ValueEur.ToString("0.00", inv)).Append(';')
              .Append(r.NetMassKg.ToString("0.000", inv)).Append(';')
              .Append(r.Quantity.ToString("0.####", inv)).Append("\r\n");
        }

        return sb.ToString();
    }
}
