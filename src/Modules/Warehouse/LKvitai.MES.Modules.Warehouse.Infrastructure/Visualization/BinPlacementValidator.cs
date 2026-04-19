using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;

public sealed class BinPlacementValidator
{
    private static readonly HashSet<string> AllowedLocationRoles =
    [
        "Cell",
        "Bulk",
        "EndCap",
        "Overflow",
        "GroundSlot"
    ];

    private readonly WarehouseDbContext _dbContext;
    private readonly RackLayoutValidator _rackLayoutValidator;

    public BinPlacementValidator(
        WarehouseDbContext dbContext,
        RackLayoutValidator rackLayoutValidator)
    {
        _dbContext = dbContext;
        _rackLayoutValidator = rackLayoutValidator;
    }

    public async Task<(RackPlacementValidationResult? Placement, string? Error)> ValidateAsync(
        int locationId,
        RackPlacementRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (null, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            return (null, "Field 'warehouseCode' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RackRowId))
        {
            return (null, "Field 'rackRowId' is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.LocationRole) &&
            !AllowedLocationRoles.Contains(request.LocationRole))
        {
            return (null, $"Field 'locationRole' has unsupported value '{request.LocationRole}'.");
        }

        var normalizedWarehouseCode = request.WarehouseCode.Trim();
        var normalizedRackRowId = request.RackRowId.Trim();
        var normalizedRole = string.IsNullOrWhiteSpace(request.LocationRole) ? null : request.LocationRole.Trim();
        var slotSpan = Math.Max(request.SlotSpan ?? 1, 1);

        var locationWarehouseId = await _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Id == locationId)
            .Select(x => (Guid?)x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);
        if (locationWarehouseId is null && !await _dbContext.Locations.AsNoTracking().AnyAsync(x => x.Id == locationId, cancellationToken))
        {
            return (null, $"Location '{locationId}' was not found.");
        }

        var layout = await _dbContext.WarehouseLayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.WarehouseCode == normalizedWarehouseCode, cancellationToken);
        if (layout is null)
        {
            return (null, $"Warehouse layout '{normalizedWarehouseCode}' was not found.");
        }

        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedWarehouseCode, cancellationToken);
        var resolvedWarehouseId = warehouse?.WarehouseId ?? locationWarehouseId;

        RackLayoutDocument rackLayout;
        try
        {
            rackLayout = _rackLayoutValidator.Parse(layout.RacksJson);
        }
        catch (Exception ex)
        {
            return (null, $"Rack config JSON is invalid: {ex.Message}");
        }

        var validationResult = _rackLayoutValidator.Validate(layout, rackLayout);
        if (!validationResult.IsValid)
        {
            return (null, JoinErrors(validationResult.Errors));
        }

        var rack = rackLayout.GetRacks()
            .FirstOrDefault(x => string.Equals(x.Id, normalizedRackRowId, StringComparison.OrdinalIgnoreCase));
        if (rack is null)
        {
            return (null, $"Rack '{normalizedRackRowId}' was not found in warehouse '{normalizedWarehouseCode}'.");
        }

        if (!RackLayoutValidator.IsSlotBased(rack.Type))
        {
            return (null, $"Rack '{normalizedRackRowId}' is type '{rack.Type}' and does not support slot placement.");
        }

        var level = rack.GetLevels().FirstOrDefault(x => x.Index == request.ShelfLevelIndex);
        if (level is null)
        {
            return (null, $"Rack '{normalizedRackRowId}' does not define level '{request.ShelfLevelIndex}'.");
        }

        if (request.SlotStart < 1 || request.SlotStart > rack.SlotsPerLevel)
        {
            return (null, $"Field 'slotStart' must be between 1 and {rack.SlotsPerLevel} for rack '{normalizedRackRowId}'.");
        }

        if ((request.SlotStart + slotSpan - 1) > rack.SlotsPerLevel)
        {
            return (null, $"Rack '{normalizedRackRowId}' level '{request.ShelfLevelIndex}' only supports {rack.SlotsPerLevel} slots.");
        }

        var endSlot = request.SlotStart + slotSpan - 1;
        var overlaps = await _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Id != locationId &&
                        x.WarehouseId == resolvedWarehouseId &&
                        x.RackRowId == normalizedRackRowId &&
                        x.ShelfLevelIndex == request.ShelfLevelIndex &&
                        x.SlotStart.HasValue)
            .AnyAsync(
                x => x.SlotStart!.Value <= endSlot &&
                     ((x.SlotStart!.Value + (x.SlotSpan ?? 1) - 1) >= request.SlotStart),
                cancellationToken);

        if (overlaps)
        {
            return (null, $"Rack '{normalizedRackRowId}' level '{request.ShelfLevelIndex}' slot range {request.SlotStart}-{endSlot} overlaps an existing bin placement.");
        }

        return (
            new RackPlacementValidationResult(
                resolvedWarehouseId ?? Guid.Empty,
                normalizedRackRowId,
                request.ShelfLevelIndex,
                request.SlotStart,
                slotSpan,
                normalizedRole),
            null);
    }

    private static string JoinErrors(IReadOnlyList<string> errors)
        => string.Join("; ", errors.Where(x => !string.IsNullOrWhiteSpace(x)));
}
