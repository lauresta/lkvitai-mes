namespace LKvitai.MES.Modules.Warehouse.Api.Observability;

public sealed class ApmOptions
{
    public const string SectionName = "Apm";

    public bool Enabled { get; set; }

    // Keep successful request volume low while always retaining failures.
    public double SuccessfulRequestSampleRate { get; set; } = 0.1d;

    public string WarehouseCodeClaimType { get; set; } = "warehouse_code";
}
