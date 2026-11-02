namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>
/// SSO (authentik, sso.lauresta.com) settings shared by every Cikada host.
///
/// Portal WebUI uses <see cref="Authority"/> + <see cref="ClientId"/> + <see cref="ClientSecret"/>
/// for the interactive OpenID Connect login. Every API validates bearer JWTs against
/// <see cref="Authority"/> and accepts the audiences in <see cref="Audiences"/> (the web client
/// plus the MCP client), so a token minted for either one reaches the same API.
/// Roles come from the <see cref="RoleClaimType"/> claim, emitted by the authentik
/// "cikada" scope from the user's <c>cikada-*</c> groups.
/// </summary>
public sealed class CikadaOidcOptions
{
    public const string SectionName = "Oidc";

    public bool Enabled { get; set; }

    /// <summary>Provider issuer, e.g. https://sso.lauresta.com/application/o/cikada/.</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Additional accepted token issuers (authentik issues per provider).</summary>
    public List<string> Issuers { get; set; } = [];

    /// <summary>Additional accepted audiences besides <see cref="ClientId"/>.</summary>
    public List<string> Audiences { get; set; } = [];

    public string Scope { get; set; } = "openid profile email cikada";

    public string RoleClaimType { get; set; } = "roles";

    /// <summary>
    /// When true, the unsigned structured "userId|roles|exp|source" bearer token is no longer
    /// accepted by APIs. Turn on once every host signs in through SSO.
    /// </summary>
    public bool DisableLegacyTokens { get; set; }

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);

    public IEnumerable<string> ValidAudiences =>
        new[] { ClientId }.Concat(Audiences).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal);
}
