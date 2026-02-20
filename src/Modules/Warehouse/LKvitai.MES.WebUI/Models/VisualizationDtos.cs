namespace LKvitai.MES.WebUI.Models;

public sealed record Visualization3dDto(
    VisualizationWarehouseDto Warehouse,
    IReadOnlyList<VisualizationBinDto> Bins,
    IReadOnlyList<VisualizationZoneDto> Zones);

public sealed record VisualizationWarehouseDto(
    string Code,
    VisualizationDimensionsDto Dimensions);

public sealed record VisualizationDimensionsDto(
    decimal Width,
    decimal Length,
    decimal Height);

public sealed record VisualizationBinDto(
    int LocationId,
    string Code,
    VisualizationCoordinateDto Coordinates,
    VisualizationBinDimensionsDto Dimensions,
    VisualizationCapacityDto Capacity,
    decimal UtilizationPercent,
    string Status,
    string Color,
    bool IsReserved,
    IReadOnlyList<VisualizationHandlingUnitDto> HandlingUnits);

public sealed record VisualizationCoordinateDto(
    decimal X,
    decimal Y,
    decimal Z);

public sealed record VisualizationCapacityDto(
    decimal? Weight,
    decimal? Volume);

public sealed record VisualizationBinDimensionsDto(
    decimal? Width,
    decimal? Length,
    decimal? Height);

public sealed record VisualizationHandlingUnitDto(
    Guid Id,
    string Lpn,
    string Sku,
    decimal Qty);

public sealed record VisualizationZoneDto(
    string Type,
    VisualizationZoneBoundsDto Bounds,
    string Color);

public sealed record VisualizationZoneBoundsDto(
    decimal X1,
    decimal Y1,
    decimal X2,
    decimal Y2);
