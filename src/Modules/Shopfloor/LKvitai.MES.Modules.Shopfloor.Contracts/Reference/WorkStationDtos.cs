namespace LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

public sealed record WorkStationDto(
    Guid Id,
    string Code,
    string Name,
    Guid WorkCenterId,
    string WorkCenterName,
    int? WipLimit,
    bool IsActive);

public sealed record CreateWorkStationRequest(
    string Code,
    string Name,
    Guid WorkCenterId,
    int? WipLimit,
    bool IsActive);

public sealed record UpdateWorkStationRequest(
    string Code,
    string Name,
    Guid WorkCenterId,
    int? WipLimit,
    bool IsActive);
