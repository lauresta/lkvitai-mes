namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record SecurityAuditLogDto(
    long Id,
    string? UserId,
    string Action,
    string Resource,
    string? ResourceId,
    string IpAddress,
    string UserAgent,
    DateTimeOffset Timestamp,
    string DetailsJson);

public sealed class SecurityAuditLogQueryDto
{
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int? Limit { get; set; }
}
