namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
public sealed record OrderAddressReferenceDto(int Id, string Name, int? ParentId, string? Path, bool HasChildren);
