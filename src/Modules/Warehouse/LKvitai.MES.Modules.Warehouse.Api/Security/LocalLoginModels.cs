namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public sealed record LocalLoginRequest(string Username, string Password);

public sealed record LocalLoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles);
