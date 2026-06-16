using LKvitai.MES.Modules.Shopfloor.Application.Services;
using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class ProductTypeMappingsEndpoints
{
    public static RouteGroupBuilder MapProductTypeMappingsEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var mappings = group.MapGroup("/product-type-mappings");

        mappings.MapGet("/coverage", async (IMappingService service, CancellationToken ct) =>
            Results.Ok(await service.GetCoverageAsync(ct).ConfigureAwait(false)));

        mappings.MapPost("/bulk-assign", async (BulkAssignMappingRequest request, IMappingService service, CancellationToken ct) =>
        {
            await service.BulkAssignAsync(request, ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        mappings.MapDelete("/{legacyCode}", async (string legacyCode, IMappingService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(legacyCode, ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        return group;
    }
}
