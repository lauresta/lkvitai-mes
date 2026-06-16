using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Application.Validation;

public sealed class CreateWorkflowTemplateRequestValidator : AbstractValidator<CreateWorkflowTemplateRequest>
{
    public CreateWorkflowTemplateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateWorkflowTemplateRequestValidator : AbstractValidator<UpdateWorkflowTemplateRequest>
{
    public UpdateWorkflowTemplateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CloneWorkflowTemplateRequestValidator : AbstractValidator<CloneWorkflowTemplateRequest>
{
    public CloneWorkflowTemplateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class BulkAssignMappingRequestValidator : AbstractValidator<BulkAssignMappingRequest>
{
    public BulkAssignMappingRequestValidator()
    {
        RuleFor(x => x.WorkflowTemplateId).NotEmpty();
        RuleFor(x => x.LegacyProductTypeCodes).NotNull().NotEmpty();
    }
}
