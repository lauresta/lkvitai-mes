using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public static class ValuationRevaluationPolicy
{
    public const decimal ApprovalThresholdEur = 1000m;

    public static Result ValidateRequest(RevaluationCommand request)
    {
        if (request.ItemId <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ItemId must be greater than 0.");
        }

        if (request.NewUnitPrice < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "NewUnitPrice must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        return Result.Ok();
    }

    /// <summary>A revaluation that lowers the value by more than 1000 EUR needs a named approver with a manager role.</summary>
    public static Result ValidateApproval(decimal valueReductionEur, string? approvedBy, bool canApproveLarge)
    {
        if (valueReductionEur <= ApprovalThresholdEur)
        {
            return Result.Ok();
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Approval required for revaluation > 1000 EUR.");
        }

        if (!canApproveLarge)
        {
            return Result.Fail(DomainErrorCodes.Forbidden, "Manager approval is required for revaluation > 1000 EUR.");
        }

        return Result.Ok();
    }
}
