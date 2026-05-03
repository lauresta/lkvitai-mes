using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Portal.WebUI.Services;

/// <summary>
/// Forwards the Portal-issued <c>warehouse_access_token</c> claim onto every
/// outgoing request to the Portal API as <c>Authorization: Bearer &lt;token&gt;</c>.
/// Mirrors <c>SalesApiAuthHandler</c> / <c>WarehouseApiAuthHandler</c> intentionally
/// so the cross-module auth contract stays uniform: Portal cookie carries the
/// structured bearer, every WebUI module forwards the same bearer to the
/// matching API, every API decodes it with PortalStructuredBearer.
///
/// In Blazor Server, after the SignalR circuit takes over from the initial
/// HTTP request, <c>HttpContextAccessor.HttpContext</c> is <c>null</c>, so a
/// cookie-from-HttpContext path only works during prerender and silently
/// breaks for every later user-driven call. <see cref="AuthenticationStateProvider"/>
/// stays available for the full circuit lifetime.
/// </summary>
public sealed class PortalApiAuthHandler : DelegatingHandler
{
    private const string TokenClaimType = "warehouse_access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;

    public PortalApiAuthHandler(
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
