using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouse/v1/item-price-snapshots")]
public sealed class ItemPriceSnapshotsController(IItemPriceSnapshotService prices) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<ItemPriceSnapshot>>> GetAsync(
        [FromBody] ItemPriceSnapshotsRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest("A request body is required.");
        var ids = request.ItemIds ?? [];
        var skus = request.Skus ?? [];
        if (ids.Count + skus.Count > 2000) return BadRequest("At most 2000 keys are allowed.");
        return Ok(await prices.GetAsync(ids, skus, ct));
    }
}

public sealed record ItemPriceSnapshotsRequest(IReadOnlyCollection<int>? ItemIds, IReadOnlyCollection<string>? Skus);
