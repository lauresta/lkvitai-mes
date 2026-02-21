using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public interface IOAuthTokenValidator
{
    Task<OAuthTokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record OAuthTokenValidationResult(
    bool IsSuccess,
    ClaimsPrincipal? Principal,
    string ErrorMessage,
    string UserId,
    IReadOnlyList<string> Roles,
    DateTimeOffset? ExpiresAt);

public sealed class OAuthTokenValidator : IOAuthTokenValidator
{
    private readonly IOAuthOpenIdConfigurationProvider _configurationProvider;
    private readonly IOAuthRoleMapper _roleMapper;
    private readonly IOptionsMonitor<OAuthOptions> _optionsMonitor;
    private readonly ILogger<OAuthTokenValidator> _logger;

    public OAuthTokenValidator(
        IOAuthOpenIdConfigurationProvider configurationProvider,
        IOAuthRoleMapper roleMapper,
        IOptionsMonitor<OAuthOptions> optionsMonitor,
        ILogger<OAuthTokenValidator> logger)
    {
        _configurationProvider = configurationProvider;
        _roleMapper = roleMapper;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<OAuthTokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Failure("Token is required.");
        }

        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return Failure("OAuth is not enabled.");
        }

        if (!IsSupportedProvider(options.Provider))
        {
            return Failure("OAuth provider must be AzureAD or Okta.");
        }

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            return Failure("OAuth authority is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            return Failure("OAuth client id is not configured.");
        }

        try
        {
            var configuration = await _configurationProvider.GetConfigurationAsync(options, cancellationToken);
            var validationParameters = BuildValidationParameters(options, configuration);

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            var mappedPrincipal = BuildPrincipalWithMappedRoles(principal, options);
            var userId = ResolveUserId(mappedPrincipal);
            var expiresAt = ResolveExpiresAt(validatedToken);
            var roles = mappedPrincipal.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            _logger.LogInformation(
                "OAuth token validated successfully. UserId={UserId}, Roles={Roles}",
                userId,
                string.Join(",", roles));

            return new OAuthTokenValidationResult(true, mappedPrincipal, string.Empty, userId, roles, expiresAt);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "OAuth token validation failed: token expired.");
            return Failure("Token expired");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "OAuth token validation failed with security token error.");
            return Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth token validation failed unexpectedly.");
            return Failure("Token validation failed.");
        }
    }

    private static bool IsSupportedProvider(string provider)
        => string.Equals(provider, "AzureAD", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider, "Okta", StringComparison.OrdinalIgnoreCase);

    private static OAuthTokenValidationResult Failure(string message)
        => new(false, null, message, string.Empty, [], null);

    private static TokenValidationParameters BuildValidationParameters(OAuthOptions options, Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration configuration)
    {
        var validIssuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            configuration.Issuer,
            options.Authority.Trim().TrimEnd('/')
        };

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuer = true,
            ValidIssuers = validIssuers,
            ValidateAudience = true,
            ValidAudience = options.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    private ClaimsPrincipal BuildPrincipalWithMappedRoles(ClaimsPrincipal principal, OAuthOptions options)
    {
        var identity = new ClaimsIdentity(principal.Claims, WarehouseAuthenticationDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        var mappedRoles = _roleMapper.MapRoles(identity.Claims, options);

        foreach (var role in mappedRoles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            identity.AddClaim(new Claim("role", role));
        }

        var userId = ResolveUserId(new ClaimsPrincipal(identity));
        if (!identity.HasClaim(x => x.Type == ClaimTypes.NameIdentifier))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (!identity.HasClaim(x => x.Type == ClaimTypes.Name))
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, userId));
        }

        return new ClaimsPrincipal(identity);
    }

    private static string ResolveUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue("sub")
               ?? principal.FindFirstValue("oid")
               ?? principal.FindFirstValue("preferred_username")
               ?? principal.FindFirstValue(ClaimTypes.Email)
               ?? "oauth-user";
    }

    private static DateTimeOffset? ResolveExpiresAt(SecurityToken validatedToken)
    {
        if (validatedToken.ValidTo == DateTime.MinValue)
        {
            return null;
        }

        var utc = DateTime.SpecifyKind(validatedToken.ValidTo, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}
