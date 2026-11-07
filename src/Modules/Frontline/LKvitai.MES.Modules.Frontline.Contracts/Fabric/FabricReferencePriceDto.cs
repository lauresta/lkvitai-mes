namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

public sealed record FabricReferencePriceDto(string TypeName, string? Unit, decimal Price, bool IsFrom);
