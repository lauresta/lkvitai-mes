namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record VerifyResultDto
{
    public bool ChecksumMatch { get; init; }
    public string ProductionChecksum { get; init; } = string.Empty;
    public string ShadowChecksum { get; init; } = string.Empty;
    public int ProductionRowCount { get; init; }
    public int ShadowRowCount { get; init; }
}
