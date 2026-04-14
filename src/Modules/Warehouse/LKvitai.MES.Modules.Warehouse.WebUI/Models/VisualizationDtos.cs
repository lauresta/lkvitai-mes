namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record Visualization3dDto(
    VisualizationWarehouseDto Warehouse,
    IReadOnlyList<VisualizationBinDto> Bins,
    IReadOnlyList<VisualizationRackDto> Racks,
    IReadOnlyList<VisualizationSlotDto> Slots,
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
    string? Address,
    string? RackId,
    int? Level,
    int? StartSlot,
    int? Span,
    string? LocationRole,
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

public sealed record VisualizationRackDto(
    string Id,
    string Type,
    VisualizationCoordinateDto Origin,
    VisualizationRackDimensionsDto Dimensions,
    decimal OrientationDeg,
    int SlotsPerLevel,
    int BayCount,
    bool BackToBack,
    string? PairedWithRackId,
    IReadOnlyList<VisualizationRackLevelDto> Levels);

public sealed record VisualizationRackLevelDto(
    int Index,
    decimal HeightFromBase);

public sealed record VisualizationRackDimensionsDto(
    decimal Width,
    decimal Depth,
    decimal Height);

public sealed record VisualizationSlotDto(
    string Address,
    string RackId,
    int Level,
    int Slot,
    bool Occupied,
    VisualizationCoordinateDto Origin,
    VisualizationRackDimensionsDto Dimensions);

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
