using LKvitai.MES.Modules.Shopfloor.Application.Services;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class WorkflowsEndpoints
{
    public static RouteGroupBuilder MapWorkflowsEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var workflows = group.MapGroup("/workflows");

        workflows.MapGet("/", async (IWorkflowService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct).ConfigureAwait(false)));

        workflows.MapGet("/{id:guid}", async (Guid id, IWorkflowService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct).ConfigureAwait(false)));

        workflows.MapPost("/", async (CreateWorkflowTemplateRequest request, IWorkflowService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return Results.Created($"/api/shopfloor/workflows/{created.Id}", created);
        });

        workflows.MapPut("/{id:guid}", async (Guid id, UpdateWorkflowTemplateRequest request, IWorkflowService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct).ConfigureAwait(false)));

        workflows.MapDelete("/{id:guid}", async (Guid id, IWorkflowService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        workflows.MapPut("/{id:guid}/graph", async (Guid id, SaveWorkflowGraphRequest request, IWorkflowService service, CancellationToken ct) =>
            Results.Ok(await service.SaveGraphAsync(id, request, ct).ConfigureAwait(false)));

        workflows.MapPost("/{id:guid}/publish", async (Guid id, IWorkflowService service, CancellationToken ct) =>
            Results.Ok(await service.PublishAsync(id, ct).ConfigureAwait(false)));

        workflows.MapPost("/{id:guid}/clone", async (Guid id, CloneWorkflowTemplateRequest request, IWorkflowService service, CancellationToken ct) =>
        {
            var created = await service.CloneAsync(id, request, ct).ConfigureAwait(false);
            return Results.Created($"/api/shopfloor/workflows/{created.Id}", created);
        });

        return group;
    }
}
