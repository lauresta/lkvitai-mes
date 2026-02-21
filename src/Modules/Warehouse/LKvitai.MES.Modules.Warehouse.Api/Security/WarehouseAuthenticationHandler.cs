using System.Security.Claims;
using System.Text.Encodings.Web;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public static class WarehouseAuthenticationDefaults
{
    public const string Scheme = "WarehouseHeader";
}

/// <summary>
/// Lightweight authentication handler for warehouse APIs.
/// Accepts either:
/// - X-User-Id + X-User-Roles headers
/// - Authorization: Bearer userId|Role1,Role2
/// </summary>
public sealed class WarehouseAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string DevUserId = "dev-user";
    private const string DevRoles =
        $"{WarehouseRoles.Operator}," +
        $"{WarehouseRoles.QCInspector}," +
        $"{WarehouseRoles.WarehouseManager}," +
        $"{WarehouseRoles.WarehouseAdmin}," +
        $"{WarehouseRoles.InventoryAccountant}," +
        $"{WarehouseRoles.CFO}";

    public WarehouseAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKeyValue = Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(apiKeyValue))
        {
            var apiKeyService = Context.RequestServices.GetService<IApiKeyService>();
            if (apiKeyService is null)
            {
                return AuthenticateResult.Fail("API key service is not configured.");
            }

            var validation = await apiKeyService.ValidateAsync(apiKeyValue, Context.RequestAborted);
            if (!validation.IsSuccess)
            {
                return AuthenticateResult.Fail(validation.ErrorMessage ?? "Invalid API key.");
            }

            var apiKeyClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, $"api-key:{validation.ApiKeyId}"),
                new(ClaimTypes.Name, $"api-key:{validation.ApiKeyId}"),
                new("auth_source", "api_key"),
                new("api_key_id", validation.ApiKeyId.ToString()),
                new("api_key_name", validation.Name),
                new("api_key_rate_limit", validation.RateLimitPerMinute.ToString()),
                new("mfa_verified", "true")
            };

            foreach (var scope in validation.Scopes)
            {
                apiKeyClaims.Add(new Claim("api_scope", scope));
            }

            var apiKeyIdentity = new ClaimsIdentity(apiKeyClaims, WarehouseAuthenticationDefaults.Scheme);
            var apiKeyPrincipal = new ClaimsPrincipal(apiKeyIdentity);
            var apiKeyTicket = new AuthenticationTicket(apiKeyPrincipal, WarehouseAuthenticationDefaults.Scheme);
            return AuthenticateResult.Success(apiKeyTicket);
        }

        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        var rolesValue = Request.Headers["X-User-Roles"].FirstOrDefault();
        string? authSource = null;
        bool? mfaVerified = null;

        if (string.IsNullOrWhiteSpace(userId) &&
            Request.Headers.Authorization.FirstOrDefault() is { } authHeader &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (LooksLikeJwt(token))
            {
                var tokenValidator = Context.RequestServices.GetService<IOAuthTokenValidator>();
                if (tokenValidator is null)
                {
                    return AuthenticateResult.Fail("OAuth token validation service is not configured.");
                }

                var validation = await tokenValidator.ValidateAsync(token, Context.RequestAborted);
                if (!validation.IsSuccess || validation.Principal is null)
                {
                    return AuthenticateResult.Fail(validation.ErrorMessage);
                }

                var jwtClaims = validation.Principal.Claims.ToList();
                if (!jwtClaims.Any(x => x.Type == "auth_source"))
                {
                    jwtClaims.Add(new Claim("auth_source", "oauth"));
                }

                if (!jwtClaims.Any(x => x.Type == "mfa_verified"))
                {
                    jwtClaims.Add(new Claim("mfa_verified", "false"));
                }

                var jwtPrincipal = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        jwtClaims,
                        WarehouseAuthenticationDefaults.Scheme,
                        ClaimTypes.Name,
                        ClaimTypes.Role));

                var jwtTicket = new AuthenticationTicket(jwtPrincipal, WarehouseAuthenticationDefaults.Scheme);
                return AuthenticateResult.Success(jwtTicket);
            }

            if (TryParseStructuredToken(token, out var parsed))
            {
                userId = parsed.UserId;
                rolesValue = string.Join(',', parsed.Roles);
                authSource = parsed.AuthSource;
                mfaVerified = parsed.MfaVerified;

                if (parsed.ExpiresAt.HasValue && DateTimeOffset.UtcNow > parsed.ExpiresAt.Value)
                {
                    return AuthenticateResult.Fail("Token expired");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            var hostEnvironment = Context.RequestServices.GetService<IHostEnvironment>();
            if (hostEnvironment?.IsDevelopment() == true)
            {
                userId = DevUserId;
                rolesValue = DevRoles;
            }
            else
            {
                return AuthenticateResult.NoResult();
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        if (!string.IsNullOrWhiteSpace(rolesValue))
        {
            foreach (var role in rolesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }
        }

        if (!string.IsNullOrWhiteSpace(authSource))
        {
            claims.Add(new Claim("auth_source", authSource));
        }

        if (mfaVerified.HasValue)
        {
            claims.Add(new Claim("mfa_verified", mfaVerified.Value ? "true" : "false"));
        }

        var identity = new ClaimsIdentity(claims, WarehouseAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, WarehouseAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private static bool LooksLikeJwt(string token)
    {
        return token.Count(x => x == '.') == 2;
    }

    private static bool TryParseStructuredToken(string token, out ParsedToken parsedToken)
    {
        parsedToken = ParsedToken.Empty;

        var segments = token.Split('|', StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0]))
        {
            return false;
        }

        var roles = Array.Empty<string>();
        if (segments.Length > 1 && !string.IsNullOrWhiteSpace(segments[1]))
        {
            roles = segments[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        DateTimeOffset? expiresAt = null;
        if (segments.Length > 2 && long.TryParse(segments[2], out var expUnix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        }

        var authSource = segments.Length > 3 && !string.IsNullOrWhiteSpace(segments[3])
            ? segments[3]
            : null;

        var mfaVerified = segments.Length > 4 &&
                          string.Equals(segments[4], "mfa", StringComparison.OrdinalIgnoreCase);

        parsedToken = new ParsedToken(segments[0], roles, expiresAt, authSource, mfaVerified);
        return true;
    }

    private sealed record ParsedToken(
        string UserId,
        IReadOnlyList<string> Roles,
        DateTimeOffset? ExpiresAt,
        string? AuthSource,
        bool MfaVerified)
    {
        public static ParsedToken Empty { get; } = new(string.Empty, [], null, null, false);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Unauthorized,
            "Authentication is required.",
            Context);

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(problemDetails);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Forbidden,
            "You do not have permission to access this resource.",
            Context);

        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(problemDetails);
    }
}
