using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EfAsync = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;
using MartenAsync = Marten.QueryableExtensions;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1")]
public sealed class WarehouseVisualizationController : ControllerBase
{
    private static readonly Meter Meter = new("LKvitai.MES.Visualization3D");
    private static readonly Counter<long> ApiCallsTotal =
        Meter.CreateCounter<long>("visualization_3d_api_calls_total");
    private static readonly Histogram<double> ApiDurationMs =
        Meter.CreateHistogram<double>("visualization_3d_api_duration_ms");

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IActiveHardLocksRepository _hardLocksRepository;
    private readonly ILogger<WarehouseVisualizationController> _logger;

    public WarehouseVisualizationController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        IActiveHardLocksRepository hardLocksRepository,
        ILogger<WarehouseVisualizationController> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _hardLocksRepository = hardLocksRepository;
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
            if (zone.X2 <= zone.X1 || zone.Y2 <= zone.Y1)
            {
                return ValidationFailure("Each zone must satisfy x2 > x1 and y2 > y1.");
            }
        }

        var normalizedWarehouseCode = request.WarehouseCode.Trim();
        var layout = await EfAsync.FirstOrDefaultAsync(
            _dbContext.WarehouseLayouts.Include(x => x.Zones),
            x => x.WarehouseCode == normalizedWarehouseCode,
            cancellationToken);

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

        layout.Zones.Clear();
        foreach (var zone in request.Zones ?? Array.Empty<UpsertZoneRequest>())
        {
            layout.Zones.Add(new ZoneDefinition
            {
                ZoneType = zone.Type.Trim().ToUpperInvariant(),
                X1 = decimal.Round(zone.X1, 2, MidpointRounding.AwayFromZero),
                Y1 = decimal.Round(zone.Y1, 2, MidpointRounding.AwayFromZero),
                X2 = decimal.Round(zone.X2, 2, MidpointRounding.AwayFromZero),
                Y2 = decimal.Round(zone.Y2, 2, MidpointRounding.AwayFromZero),
                Color = zone.Color.Trim()
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapLayout(layout));
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

        var locationsWithCoordinates = await EfAsync.ToListAsync(
            _dbContext.Locations
                .AsNoTracking()
                .Where(x =>
                    !x.IsVirtual &&
                    x.CoordinateX.HasValue &&
                    x.CoordinateY.HasValue &&
                    x.CoordinateZ.HasValue)
                .OrderBy(x => x.Code),
            cancellationToken);

        var visualizationLocations = locationsWithCoordinates
            .Select(location => new VisualizationLocationSeed(
                location,
                location.CoordinateX!.Value,
                location.CoordinateY!.Value,
                location.CoordinateZ!.Value))
            .ToList();

        if (visualizationLocations.Count == 0)
        {
            var fallbackLocations = await EfAsync.ToListAsync(
                _dbContext.Locations
                    .AsNoTracking()
                    .Where(x => !x.IsVirtual)
                    .OrderBy(x => x.Code),
                cancellationToken);

            if (fallbackLocations.Count == 0)
            {
                fallbackLocations = await EfAsync.ToListAsync(
                    _dbContext.Locations
                        .AsNoTracking()
                        .OrderBy(x => x.Code),
                    cancellationToken);
            }

            visualizationLocations = fallbackLocations
                .Select((location, index) => new VisualizationLocationSeed(
                    location,
                    location.CoordinateX ?? (index % 12) + 1,
                    location.CoordinateY ?? ((index / 12) % 12) + 1,
                    location.CoordinateZ ?? (index / 144) + 1))
                .ToList();

            if (visualizationLocations.Count > 0)
            {
                _logger.LogWarning(
                    "3D API fallback enabled: no locations with coordinates; generated auto-layout for {LocationCount} locations",
                    visualizationLocations.Count);
            }
        }

        await using var session = _documentStore.QuerySession();
        var stockCandidates = await MartenAsync.ToListAsync(
            session.Query<AvailableStockView>(),
            cancellationToken);

        if (visualizationLocations.Count == 0)
        {
            var stockLocationCodes = stockCandidates
                .Select(x => x.Location)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            visualizationLocations = stockLocationCodes
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
                .ToList();

            if (visualizationLocations.Count > 0)
            {
                _logger.LogWarning(
                    "3D API fallback enabled: generated auto-layout from AvailableStockView for {LocationCount} locations",
                    visualizationLocations.Count);
            }
        }

        var locationCodes = visualizationLocations.Select(x => x.Location.Code).ToArray();
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

        var bins = visualizationLocations.Select(node =>
        {
            var location = node.Location;
            var hasHardLock = hardLockByLocation.TryGetValue(location.Code, out var hardLockedQty) &&
                              hardLockedQty > 0m;
            var onHandQty = qtyByLocation.GetValueOrDefault(location.Code, 0m);
            var utilization = ComputeUtilization(location, onHandQty);
            var handlingUnitsForLocation = husByLocation.GetValueOrDefault(location.Code, new List<Domain.Aggregates.HandlingUnit>());
            var (status, color) = ResolveStatusAndColor(hasHardLock, handlingUnitsForLocation.Count, utilization);

            return new VisualizationBinResponse(
                location.Code,
                new VisualizationCoordinateResponse(
                    node.X,
                    node.Y,
                    node.Z),
                new VisualizationBinDimensionsResponse(
                    location.WidthMeters,
                    location.LengthMeters,
                    location.HeightMeters),
                new VisualizationCapacityResponse(
                    location.CapacityWeight ?? location.MaxWeight,
                    location.CapacityVolume ?? location.MaxVolume),
                status,
                color,
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

        var maxCoordinate = await EfAsync.ToListAsync(
            _dbContext.Locations
                .AsNoTracking()
                .Where(x => x.CoordinateX.HasValue && x.CoordinateY.HasValue && x.CoordinateZ.HasValue)
                .Select(x => new
                {
                    X = x.CoordinateX!.Value,
                    Y = x.CoordinateY!.Value,
                    Z = x.CoordinateZ!.Value
                }),
            cancellationToken);

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
            return Math.Clamp(onHandQty / location.CapacityWeight.Value, 0m, 1m);
        }

        if (location.CapacityVolume.HasValue && location.CapacityVolume.Value > 0m)
        {
            return Math.Clamp(onHandQty / location.CapacityVolume.Value, 0m, 1m);
        }

        return onHandQty > 0m ? 0.6m : 0m;
    }

    private static (string Status, string Color) ResolveStatusAndColor(
        bool hasHardLock,
        int huCount,
        decimal utilization)
    {
        if (hasHardLock)
        {
            return ("RESERVED", "#1E90FF");
        }

        if (huCount == 0)
        {
            return ("EMPTY", "#808080");
        }

        if (utilization > 0.80m)
        {
            return ("FULL", "#FFA500");
        }

        return ("LOW", "#FFFF00");
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
            layout.UpdatedAt);
    }

    private sealed record VisualizationLocationSeed(
        Location Location,
        decimal X,
        decimal Y,
        decimal Z);

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
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
        IReadOnlyList<VisualizationZoneResponse> Zones);

    public sealed record VisualizationWarehouseResponse(
        string Code,
        VisualizationDimensionsResponse Dimensions);

    public sealed record VisualizationDimensionsResponse(
        decimal Width,
        decimal Length,
        decimal Height);

    public sealed record VisualizationBinResponse(
        string Code,
        VisualizationCoordinateResponse Coordinates,
        VisualizationBinDimensionsResponse Dimensions,
        VisualizationCapacityResponse Capacity,
        string Status,
        string Color,
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
}
