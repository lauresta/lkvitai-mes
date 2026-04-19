using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EfAsync = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;
using MartenAsync = Marten.QueryableExtensions;
using Domain = LKvitai.MES.Modules.Warehouse.Domain;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1")]
public sealed class WarehouseVisualizationController : ControllerBase
{
    private const decimal LowUpperBoundUtilization = 0.45m;
    private const decimal MediumUpperBoundUtilization = 0.80m;

    private const string EmptyStatus = "EMPTY";
    private const string LowStatus = "LOW";
    private const string MediumStatus = "MEDIUM";
    private const string FullStatus = "FULL";
    private const string OverCapacityStatus = "OVER_CAPACITY";

    private static readonly string[] AllowedZoneTypes = ["RECEIVING", "STORAGE", "SHIPPING", "QUARANTINE"];
    private static readonly HashSet<string> AllowedZoneTypeSet =
        new(AllowedZoneTypes, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> StatusPalette =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [EmptyStatus] = "#D1D5DB",
            [LowStatus] = "#FDE68A",
            [MediumStatus] = "#FBBF24",
            [FullStatus] = "#F97316",
            [OverCapacityStatus] = "#DC2626"
        };

    private static readonly Meter Meter = new("LKvitai.MES.Visualization3D");
    private static readonly Counter<long> ApiCallsTotal =
        Meter.CreateCounter<long>("visualization_3d_api_calls_total");
    private static readonly Histogram<double> ApiDurationMs =
        Meter.CreateHistogram<double>("visualization_3d_api_duration_ms");

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IActiveHardLocksRepository _hardLocksRepository;
    private readonly RackLayoutValidator _rackLayoutValidator;
    private readonly WarehouseGeometryCalculator _warehouseGeometryCalculator;
    private readonly BinPlacementValidator _binPlacementValidator;
    private readonly ILogger<WarehouseVisualizationController> _logger;

    public WarehouseVisualizationController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        IActiveHardLocksRepository hardLocksRepository,
        RackLayoutValidator rackLayoutValidator,
        WarehouseGeometryCalculator warehouseGeometryCalculator,
        BinPlacementValidator binPlacementValidator,
        ILogger<WarehouseVisualizationController> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _hardLocksRepository = hardLocksRepository;
        _rackLayoutValidator = rackLayoutValidator;
        _warehouseGeometryCalculator = warehouseGeometryCalculator;
        _binPlacementValidator = binPlacementValidator;
        _logger = logger;
    }

    [HttpGet("layout")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetLayoutAsync(
        [FromQuery] string warehouseCode = "Main",
        CancellationToken cancellationToken = default)
    {
        var layout = await LoadLayoutAsync(warehouseCode, cancellationToken);
        return Ok(MapLayout(layout));
    }

    [HttpPut("layout")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> PutLayoutAsync(
        [FromBody] UpsertWarehouseLayoutRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            return ValidationFailure("warehouseCode is required.");
        }

        if (request.WidthMeters <= 0 || request.LengthMeters <= 0 || request.HeightMeters <= 0)
        {
            return ValidationFailure("widthMeters, lengthMeters, and heightMeters must be greater than zero.");
        }

        foreach (var zone in request.Zones ?? Array.Empty<UpsertZoneRequest>())
        {
            if (string.IsNullOrWhiteSpace(zone.Type))
            {
                return ValidationFailure("Each zone requires a non-empty type.");
            }

            var normalizedZoneType = zone.Type.Trim().ToUpperInvariant();
            if (!AllowedZoneTypeSet.Contains(normalizedZoneType))
            {
                return ValidationFailure($"Each zone type must be one of: {string.Join(", ", AllowedZoneTypes)}.");
            }

            if (string.IsNullOrWhiteSpace(zone.Color))
            {
                return ValidationFailure("Each zone requires a non-empty color.");
            }

            if (zone.X2 <= zone.X1 || zone.Y2 <= zone.Y1)
            {
                return ValidationFailure("Each zone must satisfy x2 > x1 and y2 > y1.");
            }
        }

        var normalizedWarehouseCode = request.WarehouseCode.Trim();
        var layout = await EfAsync.FirstOrDefaultAsync(
            _dbContext.WarehouseLayouts,
            x => x.WarehouseCode == normalizedWarehouseCode,
            cancellationToken);

        var isExistingLayout = layout is not null;
        if (layout is null)
        {
            layout = new WarehouseLayout
            {
                WarehouseCode = normalizedWarehouseCode
            };
            _dbContext.WarehouseLayouts.Add(layout);
        }

        layout.WidthMeters = decimal.Round(request.WidthMeters, 2, MidpointRounding.AwayFromZero);
        layout.LengthMeters = decimal.Round(request.LengthMeters, 2, MidpointRounding.AwayFromZero);
        layout.HeightMeters = decimal.Round(request.HeightMeters, 2, MidpointRounding.AwayFromZero);
        layout.UpdatedAt = DateTimeOffset.UtcNow;

        if (isExistingLayout)
        {
            var existingZonesQuery = _dbContext.ZoneDefinitions
                .Where(x => x.WarehouseLayoutId == layout.Id);

            if (_dbContext.Database.IsRelational())
            {
                await existingZonesQuery.ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                var existingZones = await EfAsync.ToListAsync(existingZonesQuery, cancellationToken);
                _dbContext.ZoneDefinitions.RemoveRange(existingZones);
            }
        }

        foreach (var zone in request.Zones ?? Array.Empty<UpsertZoneRequest>())
        {
            _dbContext.ZoneDefinitions.Add(new ZoneDefinition
            {
                WarehouseLayoutId = layout.Id,
                ZoneType = zone.Type.Trim().ToUpperInvariant(),
                X1 = decimal.Round(zone.X1, 2, MidpointRounding.AwayFromZero),
                Y1 = decimal.Round(zone.Y1, 2, MidpointRounding.AwayFromZero),
                X2 = decimal.Round(zone.X2, 2, MidpointRounding.AwayFromZero),
                Y2 = decimal.Round(zone.Y2, 2, MidpointRounding.AwayFromZero),
                Color = zone.Color.Trim()
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var persistedLayout = await LoadLayoutAsync(normalizedWarehouseCode, cancellationToken);
        return Ok(MapLayout(persistedLayout));
    }

    [HttpGet("warehouse-layouts/{warehouseCode}/rack-config")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetRackConfigAsync(
        string warehouseCode,
        CancellationToken cancellationToken = default)
    {
        var layout = await LoadLayoutAsync(warehouseCode, cancellationToken);
        return Ok(new RackConfigResponse(layout.WarehouseCode, layout.RacksJson, layout.UpdatedAt));
    }

    [HttpPut("warehouse-layouts/{warehouseCode}/rack-config")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> PutRackConfigAsync(
        string warehouseCode,
        [FromBody] UpdateRackConfigRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.", StatusCodes.Status422UnprocessableEntity);
        }

        var normalizedWarehouseCode = warehouseCode.Trim();

        var layout = await EfAsync.FirstOrDefaultAsync(
            _dbContext.WarehouseLayouts.Include(x => x.Zones),
            x => x.WarehouseCode == normalizedWarehouseCode,
            cancellationToken);

        if (layout is null)
        {
            return ValidationFailure($"Warehouse layout '{normalizedWarehouseCode}' was not found.", StatusCodes.Status422UnprocessableEntity);
        }

        RackLayoutDocument rackLayout;
        try
        {
            rackLayout = _rackLayoutValidator.Parse(request.RacksJson);
        }
        catch (Exception ex)
        {
            return ValidationFailure($"Rack config JSON could not be parsed: {ex.Message}", StatusCodes.Status422UnprocessableEntity);
        }

        var validation = _rackLayoutValidator.Validate(layout, rackLayout);
        if (!validation.IsValid)
        {
            return ValidationFailure(JoinValidationErrors(validation.Errors), StatusCodes.Status422UnprocessableEntity);
        }

        layout.RacksJson = string.IsNullOrWhiteSpace(request.RacksJson) ? null : request.RacksJson.Trim();
        layout.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new RackConfigResponse(layout.WarehouseCode, layout.RacksJson, layout.UpdatedAt));
    }

    [HttpPut("locations/{id:int}/rack-placement")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> PutRackPlacementAsync(
        int id,
        [FromBody] UpdateRackPlacementRequest? request,
        CancellationToken cancellationToken = default)
    {
        var location = await EfAsync.FirstOrDefaultAsync(_dbContext.Locations, x => x.Id == id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        var (placement, error) = await _binPlacementValidator.ValidateAsync(
            id,
            request is null
                ? null
                : new RackPlacementRequest(
                    request.WarehouseCode,
                    request.RackRowId,
                    request.ShelfLevelIndex,
                    request.SlotStart,
                    request.SlotSpan,
                    request.LocationRole),
            cancellationToken);

        if (error is not null || placement is null)
        {
            return ValidationFailure(error ?? "Rack placement is invalid.", StatusCodes.Status422UnprocessableEntity);
        }

        location.RackRowId = placement.RackRowId;
        location.ShelfLevelIndex = placement.ShelfLevelIndex;
        location.SlotStart = placement.SlotStart;
        location.SlotSpan = placement.SlotSpan;
        location.WarehouseId = placement.WarehouseId;
        location.LocationRole = placement.LocationRole;
        location.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new RackPlacementResponse(
            location.Id,
            request!.WarehouseCode.Trim(),
            location.RackRowId,
            location.ShelfLevelIndex,
            location.SlotStart,
            location.SlotSpan,
            location.LocationRole));
    }

    [HttpDelete("locations/{id:int}/rack-placement")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> DeleteRackPlacementAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var location = await EfAsync.FirstOrDefaultAsync(_dbContext.Locations, x => x.Id == id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        location.RackRowId = null;
        location.ShelfLevelIndex = null;
        location.SlotStart = null;
        location.SlotSpan = null;
        location.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("visualization/3d")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetVisualization3dAsync(
        [FromQuery] string warehouseCode = "Main",
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        ApiCallsTotal.Add(1);

        var layout = await LoadLayoutAsync(warehouseCode, cancellationToken);

        var warehouseEntity = await EfAsync.FirstOrDefaultAsync(
            _dbContext.Warehouses.AsNoTracking(),
            x => x.Code == layout.WarehouseCode,
            cancellationToken);

        List<Location> locations;
        if (warehouseEntity is not null)
        {
            locations = await EfAsync.ToListAsync(
                _dbContext.Locations
                    .AsNoTracking()
                    .Where(x => x.WarehouseId == warehouseEntity.WarehouseId && !x.IsVirtual)
                    .OrderBy(x => x.Code),
                cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Warehouse '{WarehouseCode}' not found in warehouses table; falling back to unowned locations",
                layout.WarehouseCode);
            locations = await EfAsync.ToListAsync(
                _dbContext.Locations
                    .AsNoTracking()
                    .Where(x => !x.IsVirtual && x.WarehouseId == null)
                    .OrderBy(x => x.Code),
                cancellationToken);
        }

        await using var session = _documentStore.QuerySession();
        var stockCandidates = await MartenAsync.ToListAsync(
            session.Query<AvailableStockView>(),
            cancellationToken);

        if (locations.Count == 0)
        {
            var stockLocationCodes = stockCandidates
                .Select(x => x.Location)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            locations = stockLocationCodes
                .Select((code, index) => new VisualizationLocationSeed(
                    new Location
                    {
                        Code = code,
                        Barcode = code,
                        Type = "Bin",
                        Status = "Active"
                    },
                    (index % 12) + 1,
                    ((index / 12) % 12) + 1,
                    (index / 144) + 1))
                .Select(x => CloneLocationWithCoordinates(x.Location, x.X, x.Y, x.Z))
                .ToList();

            if (locations.Count > 0)
            {
                _logger.LogWarning(
                    "3D API fallback enabled: generated auto-layout from AvailableStockView for {LocationCount} locations",
                    locations.Count);
            }
        }

        var resolvedLocations = ResolveVisualizationLocations(locations);
        var locationCodes = resolvedLocations.Select(x => x.Code).ToArray();
        var locationSet = new HashSet<string>(locationCodes, StringComparer.OrdinalIgnoreCase);
        var stockRows = stockCandidates
            .Where(x => locationSet.Contains(x.Location))
            .ToList();

        var qtyByLocation = stockRows
            .GroupBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => y.OnHandQty),
                StringComparer.OrdinalIgnoreCase);

        var hardLockRows = await _hardLocksRepository.GetAllActiveLocksAsync(cancellationToken);
        var hardLockByLocation = hardLockRows
            .GroupBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => y.HardLockedQty),
                StringComparer.OrdinalIgnoreCase);

        var handlingUnits = await EfAsync.ToListAsync(
            _dbContext.HandlingUnits
                .AsNoTracking()
                .OrderBy(x => x.LPN),
            cancellationToken);

        var husByLocation = handlingUnits
            .GroupBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.ToList(),
                StringComparer.OrdinalIgnoreCase);

        RackLayoutDocument rackLayout;
        try
        {
            rackLayout = _rackLayoutValidator.Parse(layout.RacksJson);
            var rackLayoutValidation = _rackLayoutValidator.Validate(layout, rackLayout);
            if (!rackLayoutValidation.IsValid)
            {
                _logger.LogWarning(
                    "Ignoring invalid rack layout for warehouse {WarehouseCode}: {Error}",
                    layout.WarehouseCode,
                    rackLayoutValidation.Errors[0]);
                rackLayout = RackLayoutDocument.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid rack JSON for warehouse {WarehouseCode}", layout.WarehouseCode);
            rackLayout = RackLayoutDocument.Empty;
        }

        var geometry = _warehouseGeometryCalculator.Calculate(
            layout.WarehouseCode,
            rackLayout,
            resolvedLocations);

        var bins = geometry.Bins.Select(node =>
        {
            var location = node.Location;
            var hasHardLock = hardLockByLocation.TryGetValue(location.Code, out var hardLockedQty) &&
                              hardLockedQty > 0m;
            var onHandQty = qtyByLocation.GetValueOrDefault(location.Code, 0m);
            var utilization = ComputeUtilization(location, onHandQty);
            var utilizationPercent = decimal.Round(utilization * 100m, 2, MidpointRounding.AwayFromZero);
            var handlingUnitsForLocation = husByLocation.GetValueOrDefault(location.Code, new List<Domain.Aggregates.HandlingUnit>());
            var status = ResolveCapacityStatus(handlingUnitsForLocation.Count, utilization);
            var color = ResolveStatusColor(status);

            return new VisualizationBinResponse(
                location.Id,
                location.Code,
                new VisualizationCoordinateResponse(
                    node.X,
                    node.Y,
                    node.Z),
                new VisualizationBinDimensionsResponse(
                    node.Width,
                    node.Length,
                    node.Height),
                new VisualizationCapacityResponse(
                    location.CapacityWeight ?? location.MaxWeight,
                    location.CapacityVolume ?? location.MaxVolume),
                utilizationPercent,
                status,
                color,
                hasHardLock,
                node.Address,
                node.RackId,
                node.Level,
                node.StartSlot,
                node.Span,
                node.LocationRole,
                handlingUnitsForLocation.Select(x =>
                {
                    var firstLine = x.Lines.FirstOrDefault();
                    return new VisualizationHandlingUnitResponse(
                        x.HUId,
                        x.LPN,
                        firstLine?.SKU ?? string.Empty,
                        firstLine?.Quantity ?? 0m);
                }).ToList());
        }).ToList();

        ApiDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        _logger.LogInformation("3D API called: {BinCount} bins returned", bins.Count);

        return Ok(new Visualization3dResponse(
            new VisualizationWarehouseResponse(
                layout.WarehouseCode,
                new VisualizationDimensionsResponse(layout.WidthMeters, layout.LengthMeters, layout.HeightMeters)),
            bins,
            geometry.Racks
                .Select(x => new VisualizationRackResponse(
                    x.Id,
                    x.Type,
                    new VisualizationCoordinateResponse(x.X, x.Y, x.Z),
                    new VisualizationRackDimensionsResponse(x.Width, x.Depth, x.Height),
                    x.OrientationDeg,
                    x.SlotsPerLevel,
                    x.BayCount,
                    x.BackToBack,
                    x.PairedWithRackId,
                    x.Levels.Select(level => new VisualizationRackLevelResponse(level.Index, level.HeightFromBase)).ToList()))
                .ToList(),
            geometry.Slots
                .Select(x => new VisualizationSlotResponse(
                    x.Address,
                    x.RackId,
                    x.Level,
                    x.Slot,
                    x.Occupied,
                    new VisualizationCoordinateResponse(x.X, x.Y, x.Z),
                    new VisualizationRackDimensionsResponse(x.Width, x.Length, x.Height)))
                .ToList(),
            layout.Zones
                .OrderBy(x => x.ZoneType)
                .Select(x => new VisualizationZoneResponse(
                    x.ZoneType,
                    new VisualizationZoneBoundsResponse(x.X1, x.Y1, x.X2, x.Y2),
                    x.Color))
                .ToList()));
    }

    private async Task<WarehouseLayout> LoadLayoutAsync(string warehouseCode, CancellationToken cancellationToken)
    {
        var normalizedWarehouseCode = string.IsNullOrWhiteSpace(warehouseCode)
            ? "Main"
            : warehouseCode.Trim();

        var layout = await EfAsync.FirstOrDefaultAsync(
            _dbContext.WarehouseLayouts
                .AsNoTracking()
                .Include(x => x.Zones),
            x => x.WarehouseCode == normalizedWarehouseCode,
            cancellationToken);

        if (layout is not null)
        {
            return layout;
        }

        Guid? fallbackWarehouseId = null;
        var fallbackWarehouse = await EfAsync.FirstOrDefaultAsync(
            _dbContext.Warehouses.AsNoTracking(),
            x => x.Code == normalizedWarehouseCode,
            cancellationToken);
        fallbackWarehouseId = fallbackWarehouse?.WarehouseId;

        IReadOnlyList<CoordinateSample> maxCoordinate;
        if (fallbackWarehouseId.HasValue)
        {
            maxCoordinate = await EfAsync.ToListAsync(
                _dbContext.Locations
                    .AsNoTracking()
                    .Where(x => x.WarehouseId == fallbackWarehouseId.Value
                                && x.CoordinateX.HasValue
                                && x.CoordinateY.HasValue
                                && x.CoordinateZ.HasValue)
                    .Select(x => new CoordinateSample(
                        x.CoordinateX!.Value,
                        x.CoordinateY!.Value,
                        x.CoordinateZ!.Value)),
                cancellationToken);
        }
        else
        {
            maxCoordinate = await EfAsync.ToListAsync(
                _dbContext.Locations
                    .AsNoTracking()
                    .Where(x => x.WarehouseId == null
                                && x.CoordinateX.HasValue
                                && x.CoordinateY.HasValue
                                && x.CoordinateZ.HasValue)
                    .Select(x => new CoordinateSample(
                        x.CoordinateX!.Value,
                        x.CoordinateY!.Value,
                        x.CoordinateZ!.Value)),
                cancellationToken);
            _logger.LogWarning(
                "LoadLayoutAsync: warehouse '{WarehouseCode}' not found; deriving dimensions from unowned locations when available",
                normalizedWarehouseCode);
        }

        var width = maxCoordinate.Count == 0 ? 50m : maxCoordinate.Max(x => x.X) + 1m;
        var length = maxCoordinate.Count == 0 ? 100m : maxCoordinate.Max(x => x.Y) + 1m;
        var height = maxCoordinate.Count == 0 ? 10m : maxCoordinate.Max(x => x.Z) + 1m;

        return new WarehouseLayout
        {
            WarehouseCode = normalizedWarehouseCode,
            WidthMeters = decimal.Round(width, 2, MidpointRounding.AwayFromZero),
            LengthMeters = decimal.Round(length, 2, MidpointRounding.AwayFromZero),
            HeightMeters = decimal.Round(height, 2, MidpointRounding.AwayFromZero),
            UpdatedAt = DateTimeOffset.UtcNow,
            Zones = new List<ZoneDefinition>()
        };
    }

    private static decimal ComputeUtilization(Location location, decimal onHandQty)
    {
        if (location.CapacityWeight.HasValue && location.CapacityWeight.Value > 0m)
        {
            return onHandQty / location.CapacityWeight.Value;
        }

        if (location.CapacityVolume.HasValue && location.CapacityVolume.Value > 0m)
        {
            return onHandQty / location.CapacityVolume.Value;
        }

        return onHandQty > 0m ? 0.6m : 0m;
    }

    private static string ResolveCapacityStatus(
        int huCount,
        decimal utilization)
    {
        if (huCount == 0)
        {
            return EmptyStatus;
        }

        if (utilization > 1m)
        {
            return OverCapacityStatus;
        }

        if (utilization > MediumUpperBoundUtilization)
        {
            return FullStatus;
        }

        if (utilization > LowUpperBoundUtilization)
        {
            return MediumStatus;
        }

        return LowStatus;
    }

    private static string ResolveStatusColor(string status)
    {
        return StatusPalette.GetValueOrDefault(status, StatusPalette[LowStatus]);
    }

    private static LayoutResponse MapLayout(WarehouseLayout layout)
    {
        return new LayoutResponse(
            layout.Id,
            layout.WarehouseCode,
            layout.WidthMeters,
            layout.LengthMeters,
            layout.HeightMeters,
            layout.Zones
                .OrderBy(x => x.ZoneType)
                .Select(x => new ZoneResponse(
                    x.Id,
                    x.ZoneType,
                    x.X1,
                    x.Y1,
                    x.X2,
                    x.Y2,
                    x.Color))
                .ToList(),
            layout.RacksJson,
            layout.UpdatedAt);
    }

    private static List<Location> ResolveVisualizationLocations(IReadOnlyList<Location> locations)
    {
        var resolved = new List<Location>(locations.Count);
        var fallbackIndex = 0;

        foreach (var location in locations)
        {
            if (!string.IsNullOrWhiteSpace(location.RackRowId) ||
                (location.CoordinateX.HasValue && location.CoordinateY.HasValue && location.CoordinateZ.HasValue))
            {
                resolved.Add(location);
                continue;
            }

            resolved.Add(CloneLocationWithCoordinates(
                location,
                (fallbackIndex % 12) + 1,
                ((fallbackIndex / 12) % 12) + 1,
                (fallbackIndex / 144) + 1));
            fallbackIndex++;
        }

        return resolved;
    }

    private static Location CloneLocationWithCoordinates(Location location, decimal x, decimal y, decimal z)
    {
        return new Location
        {
            Id = location.Id,
            Code = location.Code,
            Barcode = location.Barcode,
            Type = location.Type,
            ParentLocationId = location.ParentLocationId,
            IsVirtual = location.IsVirtual,
            MaxWeight = location.MaxWeight,
            MaxVolume = location.MaxVolume,
            Status = location.Status,
            ZoneType = location.ZoneType,
            CoordinateX = x,
            CoordinateY = y,
            CoordinateZ = z,
            WidthMeters = location.WidthMeters,
            LengthMeters = location.LengthMeters,
            HeightMeters = location.HeightMeters,
            Aisle = location.Aisle,
            Rack = location.Rack,
            Level = location.Level,
            Bin = location.Bin,
            CapacityWeight = location.CapacityWeight,
            CapacityVolume = location.CapacityVolume,
            RackRowId = location.RackRowId,
            ShelfLevelIndex = location.ShelfLevelIndex,
            SlotStart = location.SlotStart,
            SlotSpan = location.SlotSpan,
            LocationRole = location.LocationRole,
            WarehouseId = location.WarehouseId,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt,
            CreatedBy = location.CreatedBy,
            UpdatedBy = location.UpdatedBy
        };
    }

    private sealed record CoordinateSample(decimal X, decimal Y, decimal Z);

    private sealed record VisualizationLocationSeed(
        Location Location,
        decimal X,
        decimal Y,
        decimal Z);

    private ObjectResult ValidationFailure(string detail, int statusCode = StatusCodes.Status400BadRequest)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    public sealed record UpsertWarehouseLayoutRequest(
        string WarehouseCode,
        decimal WidthMeters,
        decimal LengthMeters,
        decimal HeightMeters,
        IReadOnlyList<UpsertZoneRequest> Zones);

    public sealed record UpsertZoneRequest(
        string Type,
        decimal X1,
        decimal Y1,
        decimal X2,
        decimal Y2,
        string Color);

    public sealed record LayoutResponse(
        Guid Id,
        string WarehouseCode,
        decimal WidthMeters,
        decimal LengthMeters,
        decimal HeightMeters,
        IReadOnlyList<ZoneResponse> Zones,
        string? RacksJson,
        DateTimeOffset UpdatedAt);

    public sealed record ZoneResponse(
        Guid Id,
        string Type,
        decimal X1,
        decimal Y1,
        decimal X2,
        decimal Y2,
        string Color);

    public sealed record Visualization3dResponse(
        VisualizationWarehouseResponse Warehouse,
        IReadOnlyList<VisualizationBinResponse> Bins,
        IReadOnlyList<VisualizationRackResponse> Racks,
        IReadOnlyList<VisualizationSlotResponse> Slots,
        IReadOnlyList<VisualizationZoneResponse> Zones);

    public sealed record VisualizationWarehouseResponse(
        string Code,
        VisualizationDimensionsResponse Dimensions);

    public sealed record VisualizationDimensionsResponse(
        decimal Width,
        decimal Length,
        decimal Height);

    public sealed record VisualizationBinResponse(
        int LocationId,
        string Code,
        VisualizationCoordinateResponse Coordinates,
        VisualizationBinDimensionsResponse Dimensions,
        VisualizationCapacityResponse Capacity,
        decimal UtilizationPercent,
        string Status,
        string Color,
        bool IsReserved,
        string? Address,
        string? RackId,
        int? Level,
        int? StartSlot,
        int? Span,
        string? LocationRole,
        IReadOnlyList<VisualizationHandlingUnitResponse> HandlingUnits);

    public sealed record VisualizationCoordinateResponse(
        decimal X,
        decimal Y,
        decimal Z);

    public sealed record VisualizationCapacityResponse(
        decimal? Weight,
        decimal? Volume);

    public sealed record VisualizationBinDimensionsResponse(
        decimal? Width,
        decimal? Length,
        decimal? Height);

    public sealed record VisualizationRackResponse(
        string Id,
        string Type,
        VisualizationCoordinateResponse Origin,
        VisualizationRackDimensionsResponse Dimensions,
        decimal OrientationDeg,
        int SlotsPerLevel,
        int BayCount,
        bool BackToBack,
        string? PairedWithRackId,
        IReadOnlyList<VisualizationRackLevelResponse> Levels);

    public sealed record VisualizationRackLevelResponse(
        int Index,
        decimal HeightFromBase);

    public sealed record VisualizationRackDimensionsResponse(
        decimal Width,
        decimal Depth,
        decimal Height);

    public sealed record VisualizationSlotResponse(
        string Address,
        string RackId,
        int Level,
        int Slot,
        bool Occupied,
        VisualizationCoordinateResponse Origin,
        VisualizationRackDimensionsResponse Dimensions);

    public sealed record VisualizationHandlingUnitResponse(
        Guid Id,
        string Lpn,
        string Sku,
        decimal Qty);

    public sealed record VisualizationZoneResponse(
        string Type,
        VisualizationZoneBoundsResponse Bounds,
        string Color);

    public sealed record VisualizationZoneBoundsResponse(
        decimal X1,
        decimal Y1,
        decimal X2,
        decimal Y2);

    public sealed record RackConfigResponse(
        string WarehouseCode,
        string? RacksJson,
        DateTimeOffset UpdatedAt);

    public sealed record UpdateRackConfigRequest(
        string? RacksJson);

    public sealed record UpdateRackPlacementRequest(
        string WarehouseCode,
        string RackRowId,
        int ShelfLevelIndex,
        int SlotStart,
        int? SlotSpan,
        string? LocationRole);

    public sealed record RackPlacementResponse(
        int LocationId,
        string WarehouseCode,
        string? RackRowId,
        int? ShelfLevelIndex,
        int? SlotStart,
        int? SlotSpan,
        string? LocationRole);

    private static string JoinValidationErrors(IReadOnlyList<string> errors)
        => string.Join("; ", errors.Where(x => !string.IsNullOrWhiteSpace(x)));
}
