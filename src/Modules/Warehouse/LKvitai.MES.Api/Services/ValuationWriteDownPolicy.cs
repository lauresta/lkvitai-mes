using LKvitai.MES.Application.Commands;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Api.Services;

public static class ValuationWriteDownPolicy
{
    public static Result ValidateRequest(WriteDownCommand request, decimal currentValue)
    {
        if (request.ItemId <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ItemId must be greater than 0.");
        }

        if (request.NewValue < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "NewValue must be >= 0.");
        }

        if (request.NewValue >= currentValue)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Write-down must reduce value.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Reason is required.");
        }

        return Result.Ok();
    }

    public static Result ValidateApproval(
        decimal currentValue,
        decimal newValue,
        string? approvedBy,
        bool canApproveLargeWriteDown)
    {
        var delta = decimal.Round(currentValue - newValue, 4, MidpointRounding.AwayFromZero);
        if (delta <= 1000m)
        {
            return Result.Ok();
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Approval required for write-down > $1000.");
        }

        if (!canApproveLargeWriteDown)
        {
            return Result.Fail(DomainErrorCodes.Forbidden, "Manager approval is required for write-downs > $1000.");
        }

        return Result.Ok();
    }
}
