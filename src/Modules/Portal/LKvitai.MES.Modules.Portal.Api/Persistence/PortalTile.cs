namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public sealed class PortalTile
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
    public string? Url { get; set; }
    public string? Quarter { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string[] RequiredRoles { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
