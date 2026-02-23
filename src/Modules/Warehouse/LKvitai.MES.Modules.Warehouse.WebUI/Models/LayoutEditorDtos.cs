namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record WarehouseLayoutDto(
    Guid Id,
    string WarehouseCode,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal HeightMeters,
    IReadOnlyList<WarehouseLayoutZoneDto> Zones,
    DateTimeOffset UpdatedAt);

public sealed record WarehouseLayoutZoneDto(
    Guid Id,
    string Type,
    decimal X1,
    decimal Y1,
    decimal X2,
    decimal Y2,
    string Color);

public sealed record UpdateWarehouseLayoutRequestDto(
    string WarehouseCode,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal HeightMeters,
    IReadOnlyList<UpdateWarehouseLayoutZoneRequestDto> Zones);

public sealed record UpdateWarehouseLayoutZoneRequestDto(
    string Type,
    decimal X1,
    decimal Y1,
    decimal X2,
    decimal Y2,
    string Color);
