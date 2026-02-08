using System.Reflection;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDocumentStore _documentStore;

    public DashboardController(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    [HttpGet("health")]
    public ActionResult<HealthStatusDto> GetHealth()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "dev";

        var response = new HealthStatusDto
        {
            Ok = true,
            Service = "LKvitai.MES.Api",
            Version = version,
            UtcNow = DateTime.UtcNow
        };

        return Ok(response);
    }

    [HttpGet("projection-health")]
    public async Task<ActionResult<IReadOnlyList<ProjectionHealthDto>>> GetProjectionHealthAsync(CancellationToken cancellationToken)
    {
        var allProgress = await _documentStore.Advanced.AllProjectionProgress(token: cancellationToken);
        var eventStoreStatistics = await _documentStore.Advanced.FetchEventStoreStatistics(token: cancellationToken);
        var highWaterMark = (long?)eventStoreStatistics.EventSequenceNumber;

        var projections = allProgress
            .Select(progress => new
            {
                ProjectionName = ResolveProjectionName(progress.ShardName),
                LastProcessed = (long?)progress.Sequence
            })
            .Where(progress => !string.IsNullOrWhiteSpace(progress.ProjectionName))
            .GroupBy(progress => progress.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var lastProcessed = group.Max(progress => progress.LastProcessed);
                return new ProjectionHealthDto
                {
                    ProjectionName = group.Key,
                    HighWaterMark = highWaterMark,
                    LastProcessed = lastProcessed,
                    LagSeconds = null
                };
            })
            .OrderBy(projection => projection.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(projections);
    }

    private static string ResolveProjectionName(string? shardName)
    {
        if (string.IsNullOrWhiteSpace(shardName))
        {
            return string.Empty;
        }

        var separatorIndex = shardName.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return shardName;
        }

        return shardName[..separatorIndex];
    }
}

public sealed record HealthStatusDto
{
    public bool Ok { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = "dev";
    public DateTime UtcNow { get; init; }
}

public sealed record ProjectionHealthDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public long? HighWaterMark { get; init; }
    public long? LastProcessed { get; init; }
    public double? LagSeconds { get; init; }
}
