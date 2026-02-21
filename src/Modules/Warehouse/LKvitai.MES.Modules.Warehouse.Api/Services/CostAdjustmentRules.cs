using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public static class CostAdjustmentRules
{
    public static Result ValidateRequest(AdjustCostCommand request)
    {
        if (request.ItemId <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ItemId must be greater than 0.");
        }

        if (request.NewUnitCost <= 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "NewUnitCost must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Reason must be at least 10 characters.");
        }

        return Result.Ok();
    }

    public static Result ValidateApproval(
        decimal absoluteImpact,
        Guid? approverId,
        bool hasManagerApproval,
        bool hasCfoApproval)
    {
        if (absoluteImpact <= 1000m)
        {
            return Result.Ok();
        }

        if (!approverId.HasValue || approverId.Value == Guid.Empty)
        {
            if (absoluteImpact > 10000m)
            {
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    $"CFO approval required for adjustments > $10,000 (impact: ${absoluteImpact:0.00}).");
            }

            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Manager approval required for adjustments > $1000 (impact: ${absoluteImpact:0.00}).");
        }

        if (absoluteImpact > 10000m && !hasCfoApproval)
        {
            return Result.Fail(
                DomainErrorCodes.Forbidden,
                $"CFO approval required for adjustments > $10,000 (impact: ${absoluteImpact:0.00}).");
        }

        if (absoluteImpact > 1000m && !hasManagerApproval)
        {
            return Result.Fail(
                DomainErrorCodes.Forbidden,
                $"Manager approval required for adjustments > $1000 (impact: ${absoluteImpact:0.00}).");
        }

        return Result.Ok();
    }
}
