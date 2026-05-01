using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Forwards the Portal-issued <c>warehouse_access_token</c> claim onto every
/// outgoing request to <c>SalesApi</c> as <c>Authorization: Bearer &lt;token&gt;</c>.
/// Sales.Api decodes the same token via its <c>PortalStructuredBearer</c>
/// authentication scheme, so a user signed in once on Portal is authenticated
/// against Sales.Api with no additional login flow.
///
/// Mirrors <c>WarehouseApiAuthHandler</c> intentionally so both modules share
/// the exact same auth contract with Portal:
///   1. Portal login stores <c>warehouse_access_token</c> in the cookie.
///   2. WebUI module reads that claim via <see cref="AuthenticationStateProvider"/>.
///   3. Module API decodes the structured token via PortalStructuredBearer.
///
/// Why <see cref="AuthenticationStateProvider"/> instead of
/// <see cref="IHttpContextAccessor"/>? In Blazor Server, after the SignalR
/// circuit takes over from the initial HTTP request,
/// <c>HttpContextAccessor.HttpContext</c> is <c>null</c>, so any
/// cookie-from-HttpContext forwarding only works during prerender and silently
/// breaks for every later user-driven call. <see cref="AuthenticationStateProvider"/>
/// is explicitly designed to remain available for the full circuit lifetime.
///
/// S-3 follow-up: replace with a Sales-specific structured token once Sales has
/// its own role/permission model; the wire shape will not change.
/// </summary>
public sealed class SalesApiAuthHandler : DelegatingHandler
{
    private const string TokenClaimType = "warehouse_access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;

    public SalesApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue(TokenClaimType);
        if (string.IsNullOrWhiteSpace(token))
        {
            var state = await _authStateProvider.GetAuthenticationStateAsync()
                .ConfigureAwait(false);
            token = state.User.FindFirstValue(TokenClaimType);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
