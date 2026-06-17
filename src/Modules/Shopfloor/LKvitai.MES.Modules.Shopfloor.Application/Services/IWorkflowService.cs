using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> CreateAsync(CreateWorkflowTemplateRequest request, CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> UpdateAsync(Guid id, UpdateWorkflowTemplateRequest request, CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> SaveGraphAsync(Guid id, SaveWorkflowGraphRequest request, CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> PublishAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Runs the full smart validator over the stored graph (non-destructive).</summary>
    Task<ValidationReportDto> ValidateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Runs the full smart validator over a posted graph (editor Validate / Preview).</summary>
    Task<ValidationReportDto> ValidateGraphAsync(WorkflowGraphDto graph, CancellationToken cancellationToken);

    Task<WorkflowTemplateDto> CloneAsync(Guid id, CloneWorkflowTemplateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
