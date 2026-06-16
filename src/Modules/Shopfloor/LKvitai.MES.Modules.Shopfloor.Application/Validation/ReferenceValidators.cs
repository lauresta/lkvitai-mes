using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

namespace LKvitai.MES.Modules.Shopfloor.Application.Validation;

public sealed class CreateWorkCenterRequestValidator : AbstractValidator<CreateWorkCenterRequest>
{
    public CreateWorkCenterRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
    }
}

public sealed class UpdateWorkCenterRequestValidator : AbstractValidator<UpdateWorkCenterRequest>
{
    public UpdateWorkCenterRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
    }
}

public sealed class CreateWorkStationRequestValidator : AbstractValidator<CreateWorkStationRequest>
{
    public CreateWorkStationRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.WorkCenterId).NotEmpty();
        RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0).When(x => x.WipLimit.HasValue);
    }
}

public sealed class UpdateWorkStationRequestValidator : AbstractValidator<UpdateWorkStationRequest>
{
    public UpdateWorkStationRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.WorkCenterId).NotEmpty();
        RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0).When(x => x.WipLimit.HasValue);
    }
}
