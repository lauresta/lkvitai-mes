namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record ApiKeyViewDto(
    int Id,
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt,
    bool Active,
    int RateLimitPerMinute,
    DateTimeOffset? LastUsedAt,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PreviousKeyGraceUntil,
    DateTimeOffset? UpdatedAt);

public sealed record ApiKeyCreatedDto(
    int Id,
    string Name,
    string PlainKey,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt,
    bool Active,
    int RateLimitPerMinute,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PreviousKeyGraceUntil);

public sealed class CreateApiKeyRequestDto
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; set; } = Array.Empty<string>();
    public int? RateLimitPerMinute { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
