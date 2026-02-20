using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record LandedCostAllocation(
    int ItemId,
    decimal FreightCost,
    decimal DutyCost,
    decimal InsuranceCost,
    decimal TotalLandedCost);

public static class LandedCostAllocationService
{
    public static IReadOnlyList<LandedCostAllocation> Allocate(
        IReadOnlyList<OnHandValue> rows,
        decimal freightCost,
        decimal dutyCost,
        decimal insuranceCost)
    {
        if (rows.Count == 0)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "At least one row is required for landed cost allocation.");
        }

        if (freightCost < 0m || dutyCost < 0m || insuranceCost < 0m)
        {
            throw new DomainException(DomainErrorCodes.ValidationError, "Costs must be >= 0.");
        }

        var basis = rows.Select(x => decimal.Round(x.Qty * x.UnitCost, 4, MidpointRounding.AwayFromZero)).ToArray();
        if (basis.All(x => x <= 0m))
        {
            basis = rows.Select(x => x.Qty <= 0m ? 0m : x.Qty).ToArray();
        }

        if (basis.All(x => x <= 0m))
        {
            basis = rows.Select(_ => 1m).ToArray();
        }

        var freight = AllocateByBasis(freightCost, basis);
        var duty = AllocateByBasis(dutyCost, basis);
        var insurance = AllocateByBasis(insuranceCost, basis);

        var result = new List<LandedCostAllocation>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var total = decimal.Round(freight[i] + duty[i] + insurance[i], 2, MidpointRounding.AwayFromZero);
            result.Add(new LandedCostAllocation(rows[i].ItemId, freight[i], duty[i], insurance[i], total));
        }

        return result;
    }

    private static decimal[] AllocateByBasis(decimal total, IReadOnlyList<decimal> basis)
    {
        var allocations = new decimal[basis.Count];
        if (basis.Count == 0 || total == 0m)
        {
            return allocations;
        }

        var totalBasis = basis.Sum();
        if (totalBasis <= 0m)
        {
            allocations[basis.Count - 1] = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
            return allocations;
        }

        var assigned = 0m;
        for (var i = 0; i < basis.Count - 1; i++)
        {
            var value = decimal.Round(total * (basis[i] / totalBasis), 2, MidpointRounding.AwayFromZero);
            allocations[i] = value;
            assigned += value;
        }

        allocations[basis.Count - 1] = decimal.Round(total - assigned, 2, MidpointRounding.AwayFromZero);
        return allocations;
    }
}
