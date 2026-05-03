using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Forwards the Portal-issued <c>warehouse_access_token</c> claim onto every
/// outgoing request to <c>SalesApi</c> as <c>Authorization: Bearer &lt;token&gt;</c>.
/// Sales.Api decodes the same token via its <c>PortalStructuredBearer</c>
/// authentication scheme, so a user signed in once on Portal is authenticated
/// against Sales.Api with no additional login flow.
///
/// Forwarding logic lives in <see cref="PortalBearerForwardingHandler"/>;
/// this subclass exists so Sales.WebUI's DI registers a Sales-named handler
/// (attached to the <c>SalesApi</c> HttpClient) without coupling Sales code
/// to Portal/Warehouse-side handlers.
///
/// S-3 follow-up: replace with a Sales-specific structured token once Sales
/// has its own role/permission model; the wire shape will not change.
/// </summary>
public sealed class SalesApiAuthHandler : PortalBearerForwardingHandler
{
    public SalesApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
        : base(httpContextAccessor, authStateProvider)
    {
    }
}
