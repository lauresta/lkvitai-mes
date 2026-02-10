namespace LKvitai.MES.Contracts.Messages;

public sealed record MasterDataChangedMessage
{
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ChangedBy { get; init; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; init; }
}
