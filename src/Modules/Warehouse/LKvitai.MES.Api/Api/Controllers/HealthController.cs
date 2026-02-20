using LKvitai.MES.Modules.Warehouse.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IProjectionHealthService _projectionHealthService;

    public HealthController(IProjectionHealthService projectionHealthService)
    {
        _projectionHealthService = projectionHealthService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        return await BuildHealthResponseAsync(cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("/health")]
    public Task<IActionResult> GetRootHealthAsync(CancellationToken cancellationToken = default)
    {
        return BuildHealthResponseAsync(cancellationToken);
    }

    private async Task<IActionResult> BuildHealthResponseAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _projectionHealthService.GetHealthAsync(cancellationToken);

        var projectionStatus = snapshot.ProjectionStatus
            .ToDictionary(
                x => x.Key,
                x => new ProjectionStatusDto(
                    x.Value.LastUpdated,
                    x.Value.LagEvents == 0 ? 0d : x.Value.LagSeconds,
                    x.Value.LagEvents == 0 ? "Healthy" : x.Value.Status,
                    x.Value.HighWaterMark,
                    x.Value.LastProcessed,
                    x.Value.LagEvents),
                StringComparer.OrdinalIgnoreCase);

        var projectionLagStatus = projectionStatus.Count == 0
            ? "Healthy"
            : projectionStatus.Values.Any(x => x.Status == "Unhealthy")
                ? "Unhealthy"
                : projectionStatus.Values.Any(x => x.Status == "Degraded")
                    ? "Degraded"
                    : "Healthy";

        var overallStatus = snapshot.DatabaseStatus == "Unhealthy" || snapshot.EventStoreStatus == "Unhealthy" || projectionLagStatus == "Unhealthy"
            ? "Degraded"
            : projectionLagStatus == "Degraded"
                ? "Degraded"
                : "Healthy";

        var response = new WarehouseHealthResponse(
            overallStatus,
            new HealthChecksDto(
                snapshot.DatabaseStatus,
                snapshot.EventStoreStatus,
                projectionLagStatus,
                "Healthy"),
            projectionStatus,
            snapshot.CheckedAt);

        if (projectionLagStatus == "Unhealthy" ||
            snapshot.DatabaseStatus == "Unhealthy" ||
            snapshot.EventStoreStatus == "Unhealthy")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }

    public sealed record WarehouseHealthResponse(
        string Status,
        HealthChecksDto Checks,
        IReadOnlyDictionary<string, ProjectionStatusDto> ProjectionStatus,
        DateTimeOffset CheckedAt);

    public sealed record HealthChecksDto(
        string Database,
        string EventStore,
        string ProjectionLag,
        string MessageQueue);

    public sealed record ProjectionStatusDto(
        DateTimeOffset? LastUpdated,
        double? LagSeconds,
        string Status,
        long? HighWaterMark,
        long? LastProcessed,
        long? LagEvents);
}
