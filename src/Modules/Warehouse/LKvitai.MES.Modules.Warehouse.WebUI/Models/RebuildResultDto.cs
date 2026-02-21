namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record RebuildResultDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public int EventsProcessed { get; init; }
    public string ProductionChecksum { get; init; } = string.Empty;
    public string ShadowChecksum { get; init; } = string.Empty;
    public bool ChecksumMatch { get; init; }
    public bool Swapped { get; init; }
    public TimeSpan Duration { get; init; }
}
