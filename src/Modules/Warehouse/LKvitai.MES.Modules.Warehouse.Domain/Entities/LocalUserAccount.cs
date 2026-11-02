namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Durable Cikada identity and explicitly assigned legacy SQL login.</summary>
public sealed class LocalUserAccount
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string Status { get; set; } = "Active";
    public string? LegacySqlLogin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
