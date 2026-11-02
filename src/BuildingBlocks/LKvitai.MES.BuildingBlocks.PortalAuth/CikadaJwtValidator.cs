using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>
/// Validates SSO access tokens (signed JWTs from authentik) and turns them into the claim
/// shape every Cikada API already understands: NameIdentifier, Name, Email, Role/role,
/// auth_source=oauth and mfa_verified=true (MFA is enforced by the authentik login flow).
/// </summary>
public sealed class CikadaJwtValidator
{
    public const string AuthSource = "oauth";

    private readonly IOptionsMonitor<CikadaOidcOptions> _options;
    private readonly ILogger<CikadaJwtValidator> _logger;
    private readonly object _gate = new();
    private ConfigurationManager<OpenIdConnectConfiguration>? _manager;
    private string? _managerAuthority;

    public CikadaJwtValidator(IOptionsMonitor<CikadaOidcOptions> options, ILogger<CikadaJwtValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.CurrentValue.IsConfigured;

    public bool LegacyTokensDisabled => _options.CurrentValue.Enabled && _options.CurrentValue.DisableLegacyTokens;

    public static bool LooksLikeJwt(string token) => token.Count(x => x == '.') == 2;

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, string authenticationType, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured)
        {
            return null;
        }

        try
        {
            var configuration = await GetManager(options).GetConfigurationAsync(cancellationToken);
            var issuers = new HashSet<string>(StringComparer.Ordinal) { configuration.Issuer, options.Authority };
            issuers.UnionWith(options.Issuers.Where(x => !string.IsNullOrWhiteSpace(x)));

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidIssuers = issuers,
                ValidAudiences = options.ValidAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out _);
            return new ClaimsPrincipal(BuildIdentity(principal.Claims, options, authenticationType));
        }
        catch (SecurityTokenSignatureKeyNotFoundException ex)
        {
            // Key rotation: force a metadata refresh on the next request.
            GetManager(options).RequestRefresh();
            _logger.LogWarning(ex, "SSO token signed with an unknown key.");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogInformation("SSO token rejected: {Reason}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Builds the Cikada identity from SSO claims (id token or access token).
    /// </summary>
    public static ClaimsIdentity BuildIdentity(IEnumerable<Claim> source, CikadaOidcOptions options, string authenticationType)
    {
        var claims = source.ToList();
        string? First(params string[] types) =>
            types.Select(t => claims.FirstOrDefault(c => c.Type == t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var subject = First("sub", ClaimTypes.NameIdentifier) ?? "sso-user";
        var name = First("preferred_username", "nickname", ClaimTypes.Name, "name", "email") ?? subject;
        var email = First("email", ClaimTypes.Email);

        var result = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, name),
            new("auth_source", AuthSource),
            new("mfa_verified", "true")
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            result.Add(new Claim(ClaimTypes.Email, email));
        }

        var displayName = First("name");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            result.Add(new Claim("display_name", displayName));
        }

        var roles = claims
            .Where(c => c.Type == options.RoleClaimType || c.Type == ClaimTypes.Role)
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            result.Add(new Claim(ClaimTypes.Role, role));
            result.Add(new Claim("role", role));
        }

        return new ClaimsIdentity(result, authenticationType, ClaimTypes.Name, ClaimTypes.Role);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> GetManager(CikadaOidcOptions options)
    {
        lock (_gate)
        {
            if (_manager is null || !string.Equals(_managerAuthority, options.Authority, StringComparison.Ordinal))
            {
                var metadata = options.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
                _manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadata,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true });
                _managerAuthority = options.Authority;
            }

            return _manager;
        }
    }
}
