using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// RebuildProjection command
/// [MITIGATION V-5] Triggers projection rebuild with verification
/// </summary>
public record RebuildProjectionCommand : ICommand<ProjectionRebuildReport>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    
    public string ProjectionName { get; init; } = string.Empty;
    public bool Verify { get; init; } = true;
    public bool ResetProgress { get; init; } = false;
}

/// <summary>
/// Projection rebuild report (moved from IProjectionRebuildService for command result)
/// </summary>
public record ProjectionRebuildReport
{
    public string ProjectionName { get; init; } = string.Empty;
    public int EventsProcessed { get; init; }
    public string ProductionChecksum { get; init; } = string.Empty;
    public string ShadowChecksum { get; init; } = string.Empty;
    public bool ChecksumMatch { get; init; }
    public bool Swapped { get; init; }
    public TimeSpan Duration { get; init; }
}
