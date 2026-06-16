using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Services;

/// <summary>
/// Forwards the Portal-issued structured bearer token onto every outgoing
/// request to <c>ShopfloorApi</c>. Mirrors Sales' <c>SalesApiAuthHandler</c>;
/// the forwarding logic lives in <see cref="PortalBearerForwardingHandler"/>.
/// </summary>
public sealed class ShopfloorApiAuthHandler : PortalBearerForwardingHandler
{
    public ShopfloorApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
        : base(httpContextAccessor, authStateProvider)
    {
    }
}
