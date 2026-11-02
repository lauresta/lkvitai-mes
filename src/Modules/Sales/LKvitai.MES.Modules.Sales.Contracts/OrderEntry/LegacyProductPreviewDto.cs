namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
public sealed record LegacyProductPreviewRequest(int TypeId,int WidthMm,int HeightMm,decimal Quantity,
    int? ColourId,string? Control,IReadOnlyList<string?> Parameters,bool RecalculateParameters=true,
    long? OrderId=null,long? ProductId=null);
public sealed record LegacyProductPreviewDto(IReadOnlyList<string?> Parameters,decimal? RequiredMaterial,
    decimal? AvailableMaterialForLine=null,decimal? FreeMaterial=null,int? ActionId=null,
    string? ShortageDisposition=null,bool? IsShort=null,string? MaterialUnit=null,
    string? CalculationRevision=null);
