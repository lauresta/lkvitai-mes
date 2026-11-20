namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

public sealed record PurchaseLineInput(int Key, decimal InvoiceQty, decimal InvoiceUnitPrice, bool IsService);

public sealed record PurchaseCostInput(int Key, decimal Amount, bool IncludedInCost);

/// <summary>Result of <see cref="PurchaseMath.Compute"/>; every sum is exact.</summary>
public sealed record PurchaseTotals(
    decimal RateEurPerUnit,
    decimal NetEur,
    decimal VatEur,
    decimal GrossEur,
    decimal ExpensesEur,
    IReadOnlyDictionary<int, decimal> LineFinalEur,
    IReadOnlyDictionary<int, decimal> CostEur);

/// <summary>Money rules of blueprint section 3 and the purchase arithmetic of 4.2 - 4.4. Pure functions on decimal.</summary>
public static class PurchaseMath
{
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>rateEurPerUnit = Round(1 / rateUnitsPerEur, 8); 1 for EUR.</summary>
    public static decimal RateEurPerUnit(decimal unitsPerEur)
    {
        if (unitsPerEur <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerEur), "Rate must be greater than zero.");
        }

        return decimal.Round(1m / unitsPerEur, 8, MidpointRounding.AwayFromZero);
    }

    /// <summary>eur = Round(amount x rateEurPerUnit, 2).</summary>
    public static decimal ToEur(decimal amount, decimal rateEurPerUnit) => Money(amount * rateEurPerUnit);

    /// <summary>Normalised supplier invoice number: trim, upper case, no spaces.</summary>
    public static string NormalizeInvoiceNumber(string? number)
        => string.Concat((number ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    /// <summary>
    /// Splits <paramref name="total"/> by weights; every part is rounded and the difference goes to the part with the
    /// largest value, so the parts always add up to the total.
    /// </summary>
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        var parts = new decimal[weights.Count];
        if (parts.Length == 0)
        {
            return parts;
        }

        var sum = weights.Sum();
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = sum == 0m ? Money(total / parts.Length) : Money(total * weights[i] / sum);
        }

        var largest = 0;
        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i] > parts[largest])
            {
                largest = i;
            }
        }

        parts[largest] += total - parts.Sum();
        return parts;
    }

    /// <summary>Final value of the received part of a line: Round(final x min(received, invoiced) / invoiced, 2).</summary>
    public static decimal ReceivedPortion(decimal lineFinalEur, decimal receivedQty, decimal invoiceQty)
        => invoiceQty <= 0m ? 0m : Money(lineFinalEur * Math.Min(receivedQty, invoiceQty) / invoiceQty);

    /// <summary>Blueprint 4.2 check 1 in the document currency: lines + costs must equal the net amount exactly.</summary>
    public static decimal LinesAndCostsTotal(IEnumerable<PurchaseLineInput> lines, IEnumerable<PurchaseCostInput> costs)
        => lines.Sum(l => l.InvoiceQty * l.InvoiceUnitPrice) + costs.Sum(c => c.Amount);

    public static PurchaseTotals Compute(
        decimal netAmount, decimal vatAmount, decimal unitsPerEur,
        IReadOnlyList<PurchaseLineInput> lines, IReadOnlyList<PurchaseCostInput> costs)
    {
        var rate = RateEurPerUnit(unitsPerEur);
        var netEur = ToEur(netAmount, rate);
        var vatEur = ToEur(vatAmount, rate);
        var costEur = costs.ToDictionary(c => c.Key, c => ToEur(c.Amount, rate));
        var expensesEur = costs.Where(c => !c.IncludedInCost).Sum(c => costEur[c.Key]);

        // Goods, services and costs "in cost" share netEur - expenses; services and costs then move onto the goods.
        var goods = lines.Where(l => !l.IsService).ToList();
        var pool = lines.Where(l => l.IsService).Select(l => l.InvoiceQty * l.InvoiceUnitPrice).ToList();
        var inCost = costs.Where(c => c.IncludedInCost).ToList();

        var weights = goods.Select(l => l.InvoiceQty * l.InvoiceUnitPrice)
            .Concat(pool)
            .Concat(inCost.Select(c => c.Amount)).ToList();
        var parts = Allocate(netEur - expensesEur, weights);

        var goodsParts = parts.Take(goods.Count).ToArray();
        var poolEur = parts.Skip(goods.Count).Sum();
        var shares = Allocate(poolEur, goods.Select(l => l.InvoiceQty * l.InvoiceUnitPrice).ToList());

        var finals = new Dictionary<int, decimal>();
        for (var i = 0; i < goods.Count; i++)
        {
            finals[goods[i].Key] = goodsParts[i] + shares[i];
        }

        return new PurchaseTotals(rate, netEur, vatEur, netEur + vatEur, expensesEur, finals, costEur);
    }
}
