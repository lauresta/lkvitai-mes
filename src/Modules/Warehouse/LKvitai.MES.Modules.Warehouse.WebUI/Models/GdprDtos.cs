namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record ErasureRequestDto(
    Guid Id,
    Guid CustomerId,
    string Reason,
    string Status,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? CompletedAt,
    string? RejectionReason);

public sealed class CreateErasureRequestDto
{
    public Guid CustomerId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RejectErasureRequestDto
{
    public string? RejectionReason { get; set; }
}
