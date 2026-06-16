using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Application.Validation;
using LKvitai.MES.Modules.Shopfloor.Application.Workflows;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;
using DomainWorkflowStatus = LKvitai.MES.Modules.Shopfloor.Domain.WorkflowStatus;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IWorkflowTemplateRepository _repository;
    private readonly IProductTypeMappingRepository _mappings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateWorkflowTemplateRequest> _createValidator;
    private readonly IValidator<UpdateWorkflowTemplateRequest> _updateValidator;
    private readonly IValidator<CloneWorkflowTemplateRequest> _cloneValidator;

    public WorkflowService(
        IWorkflowTemplateRepository repository,
        IProductTypeMappingRepository mappings,
        IUnitOfWork unitOfWork,
        IValidator<CreateWorkflowTemplateRequest> createValidator,
        IValidator<UpdateWorkflowTemplateRequest> updateValidator,
        IValidator<CloneWorkflowTemplateRequest> cloneValidator)
    {
        _repository = repository;
        _mappings = mappings;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _cloneValidator = cloneValidator;
    }

    public async Task<IReadOnlyList<WorkflowTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(MapSummary).ToList();
    }

    public async Task<WorkflowTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        return MapFull(template);
    }

    public async Task<WorkflowTemplateDto> CreateAsync(CreateWorkflowTemplateRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        var graphJson = WorkflowGraphMapper.Serialize(WorkflowGraphMapper.DefaultGraph());
        var template = new WorkflowTemplate(
            Guid.NewGuid(), request.Code, request.Name, request.Description, graphJson, DateTimeOffset.UtcNow);
        _repository.Add(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapFull(template);
    }

    public async Task<WorkflowTemplateDto> UpdateAsync(Guid id, UpdateWorkflowTemplateRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var template = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), id, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        template.UpdateHeader(request.Code, request.Name, request.Description, DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapFull(template);
    }

    public async Task<WorkflowTemplateDto> SaveGraphAsync(Guid id, SaveWorkflowGraphRequest request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        var domain = WorkflowGraphMapper.ToDomain(request.Graph);
        var result = WorkflowGraphValidator.ValidateLenient(domain);
        if (!result.IsValid)
        {
            throw new ShopfloorValidationException(
                "Workflow graph is invalid: " + string.Join("; ", result.Errors), result.Errors);
        }

        template.SaveGraph(WorkflowGraphMapper.Serialize(request.Graph), DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapFull(template);
    }

    public async Task<WorkflowTemplateDto> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        var graph = WorkflowGraphMapper.Deserialize(template.GraphJson);
        var domain = WorkflowGraphMapper.ToDomain(graph);
        var result = WorkflowGraphValidator.ValidateForPublish(domain);
        if (!result.IsValid)
        {
            throw new ShopfloorValidationException(
                "Workflow cannot be published: " + string.Join("; ", result.Errors), result.Errors);
        }

        template.Publish(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapFull(template);
    }

    public async Task<WorkflowTemplateDto> CloneAsync(Guid id, CloneWorkflowTemplateRequest request, CancellationToken cancellationToken)
    {
        await _cloneValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var source = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        var clone = new WorkflowTemplate(
            Guid.NewGuid(), request.Code, request.Name, request.Description, source.GraphJson, DateTimeOffset.UtcNow);
        _repository.Add(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapFull(clone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        var mappingCount = await _mappings.CountForTemplateAsync(id, cancellationToken).ConfigureAwait(false);
        if (mappingCount > 0)
        {
            throw new ShopfloorConflictException(
                $"Workflow template '{template.Code}' cannot be deleted: {mappingCount} legacy product type(s) are mapped to it.");
        }

        _repository.Remove(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowTemplateSummaryDto MapSummary(WorkflowTemplateWithStats row) => new(
        row.Template.Id,
        row.Template.Code,
        row.Template.Name,
        row.Template.Description,
        MapStatus(row.Template.Status),
        row.MappedLegacyCount,
        CountTasks(row.Template.GraphJson),
        row.Template.CreatedAt,
        row.Template.UpdatedAt);

    private static WorkflowTemplateDto MapFull(WorkflowTemplate template) => new(
        template.Id,
        template.Code,
        template.Name,
        template.Description,
        MapStatus(template.Status),
        WorkflowGraphMapper.Deserialize(template.GraphJson),
        template.CreatedAt,
        template.UpdatedAt);

    private static int CountTasks(string graphJson)
    {
        try
        {
            var graph = WorkflowGraphMapper.Deserialize(graphJson);
            return graph.Nodes.Count(n => string.Equals(n.Kind, WorkflowNodeKinds.Task, StringComparison.Ordinal));
        }
        catch (ShopfloorValidationException)
        {
            return 0;
        }
    }

    private static WorkflowStatus MapStatus(DomainWorkflowStatus status) => status switch
    {
        DomainWorkflowStatus.Draft => WorkflowStatus.Draft,
        DomainWorkflowStatus.Published => WorkflowStatus.Published,
        DomainWorkflowStatus.Archived => WorkflowStatus.Archived,
        _ => WorkflowStatus.Draft,
    };

    private static ShopfloorNotFoundException NotFound(Guid id)
        => new($"Workflow template '{id}' was not found.");

    private static ShopfloorConflictException Conflict(string code)
        => new($"A workflow template with code '{code}' already exists.");
}
