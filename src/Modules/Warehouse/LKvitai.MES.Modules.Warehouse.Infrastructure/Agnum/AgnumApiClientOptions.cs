namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumApiClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class AgnumWarehouseKeyOptions
{
    public int SndId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}
