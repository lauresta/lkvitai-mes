using LKvitai.MES.Modules.Shopfloor.Application.Services;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class WorkStationsEndpoints
{
    public static RouteGroupBuilder MapWorkStationsEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var stations = group.MapGroup("/work-stations");

        stations.MapGet("/", async (bool? activeOnly, IWorkStationService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(activeOnly ?? false, ct).ConfigureAwait(false)));

        stations.MapGet("/{id:guid}", async (Guid id, IWorkStationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct).ConfigureAwait(false)));

        stations.MapPost("/", async (CreateWorkStationRequest request, IWorkStationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct).ConfigureAwait(false);
            return Results.Created($"/api/shopfloor/work-stations/{created.Id}", created);
        });

        stations.MapPut("/{id:guid}", async (Guid id, UpdateWorkStationRequest request, IWorkStationService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct).ConfigureAwait(false)));

        stations.MapDelete("/{id:guid}", async (Guid id, IWorkStationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        return group;
    }
}
