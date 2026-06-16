using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

/// <summary>A workflow template with its derived mapping count.</summary>
public sealed record WorkflowTemplateWithStats(WorkflowTemplate Template, int MappedLegacyCount);

public interface IWorkflowTemplateRepository
{
    Task<IReadOnlyList<WorkflowTemplateWithStats>> ListAsync(CancellationToken cancellationToken);

    Task<WorkflowTemplate?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    void Add(WorkflowTemplate template);

    void Remove(WorkflowTemplate template);
}
