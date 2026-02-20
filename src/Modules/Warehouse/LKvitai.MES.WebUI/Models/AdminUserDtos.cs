namespace LKvitai.MES.WebUI.Models;

public record AdminUserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record CreateAdminUserRequestDto
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = "Active";
}

public record UpdateAdminUserRequestDto
{
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = "Active";
    public string? Email { get; init; }
}
