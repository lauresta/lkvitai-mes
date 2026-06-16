using LKvitai.MES.Modules.Shopfloor.Application.Services;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class LegacyProductTypesEndpoints
{
    public static RouteGroupBuilder MapLegacyProductTypesEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var legacy = group.MapGroup("/legacy-product-types");

        legacy.MapGet("/", async (
            string? search,
            bool? mapped,
            bool? removed,
            int? page,
            int? pageSize,
            ILegacyProductTypeService service,
            CancellationToken ct) =>
        {
            var query = new LegacyProductTypesQuery
            {
                Search = search,
                Mapped = mapped,
                Removed = removed ?? false,
                Page = page ?? 1,
                PageSize = pageSize ?? 100,
            };
            return Results.Ok(await service.QueryAsync(query, ct).ConfigureAwait(false));
        });

        legacy.MapPost("/sync", async (ILegacyProductTypeService service, CancellationToken ct) =>
            Results.Ok(await service.SyncAsync(ct).ConfigureAwait(false)));

        return group;
    }
}
