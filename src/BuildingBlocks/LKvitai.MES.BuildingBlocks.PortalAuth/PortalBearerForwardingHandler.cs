using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>
/// Shared base for every WebUI → module-API HttpClient handler that needs
/// to forward the Portal-issued <c>warehouse_access_token</c> claim as
/// <c>Authorization: Bearer &lt;token&gt;</c>. Resolves the token from the
/// current <see cref="HttpContext"/> when present (initial prerender pass)
/// and falls back to <see cref="AuthenticationStateProvider"/> after the
/// SignalR circuit takes over (when <c>HttpContextAccessor.HttpContext</c>
/// is <c>null</c>). Override <see cref="OnSendingAsync"/> if a module needs
/// extra request shaping (e.g. WarehouseApiAuthHandler prefixes the API
/// base path) — the base implementation is a no-op.
///
/// Lives in BuildingBlocks.PortalAuth so Portal/Sales/Warehouse share the
/// same code path: one place to fix bearer-refresh logic when issue #116
/// (8-hour bearer vs sliding cookie) lands.
/// </summary>
public abstract class PortalBearerForwardingHandler : DelegatingHandler
{
    private const string TokenClaimType = "warehouse_access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;

    protected PortalBearerForwardingHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
    }

    protected sealed override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await OnSendingAsync(request, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Hook for module-specific request shaping (e.g. PathBase prefixing).
    /// Runs BEFORE the bearer header is attached so subclasses can mutate
    /// the URL without worrying about ordering.
    /// </summary>
    protected virtual Task OnSendingAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
