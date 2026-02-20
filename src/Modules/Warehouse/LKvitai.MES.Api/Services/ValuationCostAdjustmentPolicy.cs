using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Api.Services;

public static class ValuationCostAdjustmentPolicy
{
    public static Result ValidateRequest(AdjustValuationCostCommand request)
    {
        if (request.ItemId <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ItemId must be greater than 0.");
        }

        if (request.NewCost < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "NewCost must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Reason must be at least 10 characters.");
        }

        return Result.Ok();
    }

    public static Result ValidateApproval(decimal oldCost, decimal newCost, string? approvedBy)
    {
        var deltaPercent = CalculateDeltaPercent(oldCost, newCost);
        if (deltaPercent <= 20m)
        {
            return Result.Ok();
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Approval required for cost change > 20%.");
        }

        return Result.Ok();
    }

    public static decimal CalculateDeltaPercent(decimal oldCost, decimal newCost)
    {
        if (oldCost == 0m)
        {
            return newCost == 0m ? 0m : 100m;
        }

        return decimal.Round(Math.Abs((newCost - oldCost) / oldCost) * 100m, 4, MidpointRounding.AwayFromZero);
    }
}
