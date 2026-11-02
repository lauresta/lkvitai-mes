using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public static class PortalOidcDefaults
{
    public const string Scheme = OpenIdConnectDefaults.AuthenticationScheme;

    /// <summary>Starts the SSO login: GET auth/sso?returnUrl=/portal/...</summary>
    public const string LoginPath = "/auth/sso";

    public const string CallbackPath = "/signin-oidc";

    /// <summary>Claim the WebUIs forward as the API bearer token.</summary>
    public const string AccessTokenClaim = "warehouse_access_token";
}

public static class PortalOidcExtensions
{
    /// <summary>
    /// Registers <see cref="CikadaOidcOptions"/> and the shared SSO JWT validator. Call from every API
    /// (and from Portal WebUI, which also calls <see cref="AddPortalOidcLogin"/>).
    /// </summary>
    public static IServiceCollection AddCikadaSsoTokenValidation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CikadaOidcOptions>(configuration.GetSection(CikadaOidcOptions.SectionName));
        services.AddSingleton<CikadaJwtValidator>();
        return services;
    }

    public static bool IsCikadaSsoEnabled(this IConfiguration configuration)
    {
        var options = configuration.GetSection(CikadaOidcOptions.SectionName).Get<CikadaOidcOptions>();
        return options?.IsConfigured == true;
    }

    /// <summary>
    /// Adds the interactive OpenID Connect login to the Portal cookie scheme. On success the
    /// shared Portal cookie carries the SSO identity, roles and the access token
    /// (<see cref="PortalOidcDefaults.AccessTokenClaim"/>) every WebUI forwards to its API.
    /// </summary>
    public static IServiceCollection AddPortalOidcLogin(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCikadaSsoTokenValidation(configuration);
        var options = configuration.GetSection(CikadaOidcOptions.SectionName).Get<CikadaOidcOptions>() ?? new CikadaOidcOptions();
        if (!options.IsConfigured)
        {
            return services;
        }

        services.AddAuthentication()
            .AddOpenIdConnect(PortalOidcDefaults.Scheme, oidc =>
            {
                oidc.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                oidc.Authority = options.Authority;
                oidc.ClientId = options.ClientId;
                oidc.ClientSecret = options.ClientSecret;
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;
                oidc.CallbackPath = PortalOidcDefaults.CallbackPath;
                oidc.SignedOutCallbackPath = "/signout-callback-oidc";
                oidc.SaveTokens = false;
                oidc.GetClaimsFromUserInfoEndpoint = false;
                oidc.MapInboundClaims = false;
                oidc.Scope.Clear();
                foreach (var scope in options.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    oidc.Scope.Add(scope);
                }

                // Query response mode returns via a top-level GET, so Lax correlation/nonce
                // cookies survive the cross-site hop from sso.lauresta.com (form_post would not).
                oidc.ResponseMode = OpenIdConnectResponseMode.Query;
                oidc.CorrelationCookie.SameSite = SameSiteMode.Lax;
                oidc.NonceCookie.SameSite = SameSiteMode.Lax;
                oidc.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        var identity = CikadaJwtValidator.BuildIdentity(
                            context.Principal?.Claims ?? [],
                            options,
                            CookieAuthenticationDefaults.AuthenticationScheme);

                        var accessToken = context.TokenEndpointResponse?.AccessToken;
                        if (!string.IsNullOrWhiteSpace(accessToken))
                        {
                            identity.AddClaim(new Claim(PortalOidcDefaults.AccessTokenClaim, accessToken));
                        }

                        var idToken = context.TokenEndpointResponse?.IdToken;
                        if (!string.IsNullOrWhiteSpace(idToken))
                        {
                            identity.AddClaim(new Claim("id_token", idToken));
                        }

                        context.Principal = new ClaimsPrincipal(identity);
                        context.Properties ??= new AuthenticationProperties();
                        context.Properties.IsPersistent = true;
                        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(PortalAuthDefaults.CookieLifetime);
                        context.Properties.AllowRefresh = false;
                        return Task.CompletedTask;
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect(
                            $"{context.Request.PathBase}{PortalAuthDefaults.LoginPath}?local=1&error=sso");
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// GET auth/sso?returnUrl=... — challenges the SSO provider. Only mapped when SSO is configured.
    /// </summary>
    public static IEndpointRouteBuilder MapPortalOidcLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(PortalOidcDefaults.LoginPath, (HttpContext context, string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = NormalizeReturnUrl(context, returnUrl) },
                [PortalOidcDefaults.Scheme]))
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// End-session URL of the SSO provider for the signed-in user, or null when SSO is off
    /// or the session was not created by SSO.
    /// </summary>
    public static string? BuildSsoLogoutUrl(HttpContext context, string postLogoutRedirect)
    {
        var options = context.RequestServices.GetService<IOptionsMonitor<CikadaOidcOptions>>()?.CurrentValue
            ?? context.RequestServices.GetRequiredService<IConfiguration>()
                .GetSection(CikadaOidcOptions.SectionName).Get<CikadaOidcOptions>();
        if (options?.IsConfigured != true ||
            !string.Equals(context.User.FindFirstValue("auth_source"), CikadaJwtValidator.AuthSource, StringComparison.Ordinal))
        {
            return null;
        }

        var url = options.Authority.TrimEnd('/') + "/end-session/";
        var idToken = context.User.FindFirstValue("id_token");
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            query.Add("id_token_hint=" + Uri.EscapeDataString(idToken));
        }

        query.Add("post_logout_redirect_uri=" + Uri.EscapeDataString(postLogoutRedirect));
        return url + "?" + string.Join('&', query);
    }

    private static string NormalizeReturnUrl(HttpContext context, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            returnUrl.StartsWith('/') &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return returnUrl;
        }

        return context.Request.PathBase.HasValue ? context.Request.PathBase + "/" : "/";
    }
}
