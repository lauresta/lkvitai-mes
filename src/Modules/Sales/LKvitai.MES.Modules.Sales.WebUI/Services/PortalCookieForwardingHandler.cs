using LKvitai.MES.BuildingBlocks.PortalAuth;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Forwards the Portal auth cookie from the current Blazor HttpContext to outbound
/// HttpClient requests against the Sales API. This mirrors the Warehouse WebUI's
/// <c>WarehouseApiAuthHandler</c> pattern in spirit (auth material carried from
/// the signed-in user to the server-side API call) while keeping Sales on the
/// shared PortalAuth cookie scheme rather than Warehouse's bearer structured-token
/// scheme. When Sales adopts real role-based authorization the forwarding will be
/// upgraded to issue a bearer token that matches WarehouseAuthenticationHandler.
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
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null &&
            httpContext.Request.Cookies.TryGetValue(PortalAuthDefaults.CookieName, out var cookieValue) &&
            !string.IsNullOrWhiteSpace(cookieValue))
        {
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"{PortalAuthDefaults.CookieName}={cookieValue}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
