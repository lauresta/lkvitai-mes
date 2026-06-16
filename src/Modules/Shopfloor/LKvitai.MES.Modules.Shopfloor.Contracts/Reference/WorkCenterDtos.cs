namespace LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

public sealed record WorkCenterDto(Guid Id, string Code, string Name);

public sealed record CreateWorkCenterRequest(string Code, string Name);

public sealed record UpdateWorkCenterRequest(string Code, string Name);
