using LKvitai.MES.Modules.Shopfloor.Application.Services;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class WorkCentersEndpoints
{
    public static RouteGroupBuilder MapWorkCentersEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var centers = group.MapGroup("/work-centers");

        centers.MapGet("/", async (IWorkCenterService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct).ConfigureAwait(false)));

        centers.MapGet("/{id:guid}", async (Guid id, IWorkCenterService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct).ConfigureAwait(false)));

        centers.MapPost("/", async (CreateWorkCenterRequest request, IWorkCenterService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return Results.Created($"/api/shopfloor/work-centers/{created.Id}", created);
        });

        centers.MapPut("/{id:guid}", async (Guid id, UpdateWorkCenterRequest request, IWorkCenterService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct).ConfigureAwait(false)));

        centers.MapDelete("/{id:guid}", async (Guid id, IWorkCenterService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        return group;
    }
}
