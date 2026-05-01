using LKvitai.MES.BuildingBlocks.PortalAuth;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Forwards the inbound Portal authentication cookie from the Blazor Server
/// <see cref="HttpContext"/> onto outgoing <see cref="HttpClient"/> requests, so
/// that server-to-server calls from Sales.WebUI to Sales.Api carry the same
/// identity the user already has on the WebUI request. Both apps share the
/// Portal DataProtection keys (see <c>PortalAuthDefaults</c>), so the cookie is
/// decryptable on the API side without any extra wiring.
///
/// TODO S-3: replace with role-aware token / scoped service identity when the
/// Sales role model is finalised.
/// </summary>
public sealed class PortalCookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PortalCookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null
            && ctx.Request.Cookies.TryGetValue(PortalAuthDefaults.CookieName, out var cookieValue)
            && !string.IsNullOrEmpty(cookieValue))
        {
            // HttpClient strips Cookie headers added via Headers.Add unless we
            // bypass the validated headers collection — TryAddWithoutValidation
            // is the correct call for cookie strings.
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                $"{PortalAuthDefaults.CookieName}={cookieValue}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
