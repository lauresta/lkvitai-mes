using System.Text.Json.Serialization;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;

public sealed record RackLayoutDocument(
    [property: JsonPropertyName("warehouseCode")] string? WarehouseCode,
    [property: JsonPropertyName("racks")] IReadOnlyList<RackRowDefinition>? Racks,
    [property: JsonPropertyName("doors")] IReadOnlyList<WarehouseDoorDefinition>? Doors)
{
    public static RackLayoutDocument Empty { get; } = new(null, Array.Empty<RackRowDefinition>(), Array.Empty<WarehouseDoorDefinition>());

    public IReadOnlyList<RackRowDefinition> GetRacks() => Racks ?? Array.Empty<RackRowDefinition>();
    public IReadOnlyList<WarehouseDoorDefinition> GetDoors() => Doors ?? Array.Empty<WarehouseDoorDefinition>();
}

public sealed record RackRowDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("origin")] RackOriginDefinition Origin,
    [property: JsonPropertyName("dimensions")] RackDimensionsDefinition Dimensions,
    [property: JsonPropertyName("orientationDeg")] decimal OrientationDeg,
    [property: JsonPropertyName("slotsPerLevel")] int SlotsPerLevel,
    [property: JsonPropertyName("bayCount")] int BayCount,
    [property: JsonPropertyName("backToBack")] bool BackToBack,
    [property: JsonPropertyName("pairedWithRackId")] string? PairedWithRackId,
    [property: JsonPropertyName("levels")] IReadOnlyList<RackLevelDefinition>? Levels)
{
    public IReadOnlyList<RackLevelDefinition> GetLevels() => Levels ?? Array.Empty<RackLevelDefinition>();
}

public sealed record RackOriginDefinition(
    [property: JsonPropertyName("x")] decimal X,
    [property: JsonPropertyName("y")] decimal Y,
    [property: JsonPropertyName("z")] decimal Z);

public sealed record RackDimensionsDefinition(
    [property: JsonPropertyName("width")] decimal Width,
    [property: JsonPropertyName("depth")] decimal Depth,
    [property: JsonPropertyName("height")] decimal Height);

public sealed record RackLevelDefinition(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("heightFromBase")] decimal HeightFromBase);

public sealed record WarehouseDoorDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("wall")] string Wall,
    [property: JsonPropertyName("offsetFromLeft")] decimal OffsetFromLeft,
    [property: JsonPropertyName("width")] decimal Width,
    [property: JsonPropertyName("height")] decimal Height,
    [property: JsonPropertyName("bottom")] decimal Bottom,
    [property: JsonPropertyName("label")] string? Label);

public sealed record VisualizationRackModel(
    string Id,
    string Type,
    decimal X,
    decimal Y,
    decimal Z,
    decimal Width,
    decimal Depth,
    decimal Height,
    decimal OrientationDeg,
    int SlotsPerLevel,
    int BayCount,
    bool BackToBack,
    string? PairedWithRackId,
    IReadOnlyList<VisualizationRackLevelModel> Levels);

public sealed record VisualizationRackLevelModel(
    int Index,
    decimal HeightFromBase);

public sealed record VisualizationSlotModel(
    string Address,
    string RackId,
    int Level,
    int Slot,
    bool Occupied,
    decimal X,
    decimal Y,
    decimal Z,
    decimal Width,
    decimal Length,
    decimal Height);

public sealed record VisualizationBinModel(
    Location Location,
    decimal X,
    decimal Y,
    decimal Z,
    decimal? Width,
    decimal? Length,
    decimal? Height,
    string? Address,
    string? RackId,
    int? Level,
    int? StartSlot,
    int? Span,
    string? LocationRole);

public sealed record WarehouseGeometryResult(
    IReadOnlyList<VisualizationRackModel> Racks,
    IReadOnlyList<VisualizationSlotModel> Slots,
    IReadOnlyList<VisualizationBinModel> Bins);

public sealed record RackLayoutValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record RackPlacementRequest(
    string WarehouseCode,
    string RackRowId,
    int ShelfLevelIndex,
    int SlotStart,
    int? SlotSpan,
    string? LocationRole);

public sealed record RackPlacementValidationResult(
    Guid WarehouseId,
    string RackRowId,
    int ShelfLevelIndex,
    int SlotStart,
    int SlotSpan,
    string? LocationRole);
