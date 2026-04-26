namespace LKvitai.MES.Modules.Portal.WebUI.Auth;

public sealed record PortalLoginRequest(string Username, string Password);

public sealed record PortalLoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles);
