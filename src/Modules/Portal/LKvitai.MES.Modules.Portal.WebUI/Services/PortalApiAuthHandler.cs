using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Portal.WebUI.Services;

/// <summary>
/// Forwards the Portal-issued <c>warehouse_access_token</c> claim to the
/// Portal API as <c>Authorization: Bearer &lt;token&gt;</c>. All forwarding
/// logic lives in <see cref="PortalBearerForwardingHandler"/>; this subclass
/// exists so the WebUI registers a Portal-named handler in DI (<c>Portal API</c>
/// HttpClient) without coupling Portal.WebUI to Sales/Warehouse handlers.
/// </summary>
public sealed class PortalApiAuthHandler : PortalBearerForwardingHandler
{
    public PortalApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
        : base(httpContextAccessor, authStateProvider)
    {
    }
}
