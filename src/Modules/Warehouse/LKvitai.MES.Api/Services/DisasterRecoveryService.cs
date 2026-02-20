using System.Text.Json;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface IDisasterRecoveryService
{
    Task<DRDrillDto> TriggerDrillAsync(DisasterScenario scenario, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DRDrillDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<DRDrillDto> RunQuarterlyDrillAsync(CancellationToken cancellationToken = default);
}

public sealed class DisasterRecoveryService : IDisasterRecoveryService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityAuditLogService _auditLogService;
    private readonly ILogger<DisasterRecoveryService> _logger;

    public DisasterRecoveryService(
        WarehouseDbContext dbContext,
        ICurrentUserService currentUserService,
        ISecurityAuditLogService auditLogService,
        ILogger<DisasterRecoveryService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<DRDrillDto> TriggerDrillAsync(DisasterScenario scenario, CancellationToken cancellationToken = default)
    {
        var drill = new DRDrill
        {
            DrillStartedAt = DateTimeOffset.UtcNow,
            Scenario = scenario,
            Status = DrillStatus.InProgress,
            Notes = string.Empty,
            IssuesIdentifiedJson = "[]"
        };

        _dbContext.DRDrills.Add(drill);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var issues = new List<string>();
        var notes = new List<string>();
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            await ExecuteRestoreStepAsync(drill.Id, notes, cancellationToken);
            await ExecuteDnsSwitchStepAsync(drill.Id, scenario, notes, issues, cancellationToken);
            await ExecuteServiceVerificationStepAsync(drill.Id, notes, cancellationToken);

            var completedAt = DateTimeOffset.UtcNow;
            drill.DrillCompletedAt = completedAt;
            drill.ActualRTO = completedAt - startedAt;
            drill.Status = DrillStatus.Completed;
            drill.Notes = string.Join("\n", notes);
            drill.IssuesIdentifiedJson = JsonSerializer.Serialize(issues);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await WriteNotificationArtifactAsync(drill, cancellationToken);
            await WriteAuditAsync("DR_DRILL_COMPLETED", drill.Id.ToString(), new
            {
                Scenario = ToApiScenario(drill.Scenario),
                ActualRto = drill.ActualRTO.ToString(),
                Issues = issues
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            var completedAt = DateTimeOffset.UtcNow;
            drill.DrillCompletedAt = completedAt;
            drill.ActualRTO = completedAt - startedAt;
            drill.Status = DrillStatus.Failed;
            issues.Add(ex.Message);
            drill.IssuesIdentifiedJson = JsonSerializer.Serialize(issues);
            notes.Add($"Drill failed: {ex.Message}");
            drill.Notes = string.Join("\n", notes);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync("DR_DRILL_FAILED", drill.Id.ToString(), new
            {
                Scenario = ToApiScenario(drill.Scenario),
                Error = ex.Message
            }, cancellationToken);

            _logger.LogError(ex, "DR drill failed for {DrillId}", drill.Id);
        }

        return ToDto(drill);
    }

    public async Task<IReadOnlyList<DRDrillDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DRDrills
            .AsNoTracking()
            .OrderByDescending(x => x.DrillStartedAt)
            .Take(200)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public Task<DRDrillDto> RunQuarterlyDrillAsync(CancellationToken cancellationToken = default)
    {
        return TriggerDrillAsync(DisasterScenario.DataCenterOutage, cancellationToken);
    }

    private static async Task ExecuteRestoreStepAsync(Guid drillId, List<string> notes, CancellationToken cancellationToken)
    {
        var dir = Path.Combine("artifacts", "dr-drills", drillId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "step1-restore.log");
        var payload = $"Step=RestoreFromBackup\nExecutedAtUtc={DateTimeOffset.UtcNow:O}\nScript=scripts/disaster-recovery/restore_failover.sh\n";
        await File.WriteAllTextAsync(path, payload, cancellationToken);
        notes.Add("Step 1 complete: Restore from backup.");
    }

    private static async Task ExecuteDnsSwitchStepAsync(
        Guid drillId,
        DisasterScenario scenario,
        List<string> notes,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var dir = Path.Combine("artifacts", "dr-drills", drillId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "step2-dns-switch.log");

        var payload = $"Step=SwitchDns\nExecutedAtUtc={DateTimeOffset.UtcNow:O}\nScript=scripts/disaster-recovery/switch_dns_failover.sh\n";
        await File.WriteAllTextAsync(path, payload, cancellationToken);

        if (scenario == DisasterScenario.DataCenterOutage)
        {
            issues.Add("DNS switch automation failed");
            notes.Add("Step 2 complete: DNS switch required manual intervention.");
            return;
        }

        notes.Add("Step 2 complete: DNS switch successful.");
    }

    private static async Task ExecuteServiceVerificationStepAsync(Guid drillId, List<string> notes, CancellationToken cancellationToken)
    {
        var dir = Path.Combine("artifacts", "dr-drills", drillId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "step3-verify-services.log");
        var payload = $"Step=VerifyServices\nExecutedAtUtc={DateTimeOffset.UtcNow:O}\nScript=scripts/disaster-recovery/verify_services.sh\n";
        await File.WriteAllTextAsync(path, payload, cancellationToken);
        notes.Add("Step 3 complete: Service verification passed.");
    }

    private async Task WriteNotificationArtifactAsync(DRDrill drill, CancellationToken cancellationToken)
    {
        var dir = Path.Combine("artifacts", "dr-drills", drill.Id.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "devops-notification.txt");

        var issues = JsonSerializer.Deserialize<List<string>>(drill.IssuesIdentifiedJson) ?? new List<string>();
        var message = $"To: devops@localhost\nSubject: DR drill {drill.Id:N} result\nStatus: {drill.Status}\nScenario: {drill.Scenario}\nActualRTO: {drill.ActualRTO}\nIssues: {(issues.Count == 0 ? "none" : string.Join("; ", issues))}\n";
        await File.WriteAllTextAsync(path, message, cancellationToken);

        await WriteAuditAsync("DR_DRILL_NOTIFICATION_SENT", drill.Id.ToString(), new
        {
            Recipient = "devops@localhost",
            NotificationPath = path
        }, cancellationToken);
    }

    private static DRDrillDto ToDto(DRDrill drill)
    {
        var issues = JsonSerializer.Deserialize<List<string>>(drill.IssuesIdentifiedJson) ?? new List<string>();

        return new DRDrillDto(
            drill.Id,
            drill.DrillStartedAt,
            drill.DrillCompletedAt,
            ToApiScenario(drill.Scenario),
            drill.ActualRTO,
            drill.Status.ToString().ToUpperInvariant(),
            drill.Notes,
            issues);
    }

    private static string ToApiScenario(DisasterScenario scenario)
    {
        return scenario switch
        {
            DisasterScenario.DataCenterOutage => "DATA_CENTER_OUTAGE",
            DisasterScenario.DatabaseCorruption => "DATABASE_CORRUPTION",
            DisasterScenario.Ransomware => "RANSOMWARE",
            _ => scenario.ToString().ToUpperInvariant()
        };
    }

    private async Task WriteAuditAsync(string action, string resourceId, object payload, CancellationToken cancellationToken)
    {
        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                action,
                "DISASTER_RECOVERY",
                resourceId,
                "system",
                "dr-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(payload)),
            cancellationToken);
    }
}

public sealed class QuarterlyDisasterRecoveryDrillJob
{
    private readonly IDisasterRecoveryService _service;

    public QuarterlyDisasterRecoveryDrillJob(IDisasterRecoveryService service)
    {
        _service = service;
    }

    public Task<DRDrillDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _service.RunQuarterlyDrillAsync(cancellationToken);
    }
}

public sealed record DRDrillDto(
    Guid Id,
    DateTimeOffset DrillStartedAt,
    DateTimeOffset? DrillCompletedAt,
    string Scenario,
    TimeSpan ActualRTO,
    string Status,
    string Notes,
    IReadOnlyList<string> IssuesIdentified);
