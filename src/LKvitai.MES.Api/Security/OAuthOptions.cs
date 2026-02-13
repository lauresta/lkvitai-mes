namespace LKvitai.MES.Api.Security;

public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "AzureAD";
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid profile email";
    public string CallbackPath { get; set; } = "/api/auth/oauth/callback";
    public string RoleClaimType { get; set; } = "groups";
    public string DefaultRole { get; set; } = WarehouseRoles.Operator;
    public int SessionTimeoutHours { get; set; } = 8;
    public bool AllowInsecureMetadata { get; set; }
    public Dictionary<string, string> RoleMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record OAuthLoginState(string State, string CodeVerifier, string? ReturnUrl, DateTimeOffset CreatedAt);
