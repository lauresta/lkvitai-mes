using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

/// <summary>User profiles govern SQL impersonation. Unsigned legacy role headers
/// alone must not authorize creation, role changes or mapping changes.</summary>
public sealed class TrustedUserAdministrationFilter(CikadaUserBridgeProof proof,IAdminUserStore users, IOptionsMonitor<CikadaOidcOptions>? options = null) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var http=context.HttpContext;
        var identity=proof.Read(http.Request.Headers[CikadaUserBridgeProof.HeaderName].ToString(),CikadaUserBridgeProof.ManageUsers);
        var actor=users.GetAll().FirstOrDefault(x=>x.Id==identity?.UserId);
        Guid? authenticatedId;
        if (http.User.FindFirstValue("auth_source") == CikadaJwtValidator.AuthSource)
        {
            var sso = options is null ? null : CikadaSsoAccountProof.FromValidatedPrincipal(http.User,
                options.CurrentValue, WarehouseAuthenticationDefaults.Scheme);
            authenticatedId = sso is null ? null : users.FindActiveByUsername(sso.Username)?.Id;
        }
        else authenticatedId = Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var nativeId) ? nativeId : null;
        if(identity is null || authenticatedId!=identity.UserId
            || !http.User.IsInRole(WarehouseRoles.WarehouseAdmin) || actor is null || actor.Status!=AdminUserRecord.ActiveStatus
            || !actor.Roles.Contains(WarehouseRoles.WarehouseAdmin,StringComparer.OrdinalIgnoreCase))
        {
            context.Result=new ObjectResult(new ProblemDetails{Status=403,Title="A verified Cikada administrator session is required."}){StatusCode=403};
            return;
        }
        await next();
    }
}
