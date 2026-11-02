using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Microsoft.Extensions.Options;
using Moq;

namespace LKvitai.MES.Tests.Warehouse.Unit;
public sealed class TrustedUserAdministrationTests
{
    [Theory]
    [InlineData("valid",true)]
    [InlineData("missing",false)]
    [InlineData("wrong-purpose",false)]
    [InlineData("different-actor",false)]
    [InlineData("inactive",false)]
    [InlineData("role-revoked",false)]
    [InlineData("forged-admin",false)]
    public async Task Mapping_and_user_management_require_current_signed_administrator(string mode,bool expected)
    {
        var users=new InMemoryAdminUserStore();var admin=users.GetAll().Single();
        var actor=admin.Id;
        if(mode=="inactive")users.TryUpdate(actor,new UpdateAdminUserRequest(admin.Roles,"Inactive"),out _,out _);
        if(mode=="role-revoked")users.TryUpdate(actor,new UpdateAdminUserRequest([WarehouseRoles.Operator],"Active"),out _,out _);
        var codec=new CikadaUserBridgeProof(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var http=new DefaultHttpContext();
        http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,WarehouseRoles.WarehouseAdmin)],"WarehouseHeader"));
        if(mode!="missing")http.Request.Headers[CikadaUserBridgeProof.HeaderName]=mode=="forged-admin"?"untrusted":codec.Create(mode=="different-actor"?Guid.NewGuid():actor,mode=="wrong-purpose"?CikadaUserBridgeProof.LookupLegacySql:CikadaUserBridgeProof.ManageUsers);
        var action=new ActionContext(http,new RouteData(),new ActionDescriptor(),new ModelStateDictionary());
        var context=new ActionExecutingContext(action,[],new Dictionary<string,object?>(),new object());
        var executed=false;
        await new TrustedUserAdministrationFilter(codec,users).OnActionExecutionAsync(context,()=>{executed=true;return Task.FromResult(new ActionExecutedContext(action,[],new object()));});
        Assert.Equal(expected,executed);
        if(!expected)Assert.Equal(403,Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }
    [Theory]
    [InlineData("external-subject", true)]
    [InlineData("a277b0ad-1503-42c5-a765-bf16befbbaf1", true)]
    [InlineData("missing-account", false)]
    [InlineData("inactive", false)]
    [InlineData("idp-not-admin", false)]
    [InlineData("wrong-actor", false)]
    [InlineData("unsigned-source", false)]
    public async Task Sso_administration_binds_current_durable_account_and_keeps_both_admin_checks(string mode, bool expected)
    {
        var users = new InMemoryAdminUserStore(); var admin = users.GetAll().Single();
        if (mode == "inactive") users.TryUpdate(admin.Id, new UpdateAdminUserRequest(admin.Roles, "Inactive"), out _, out _);
        var settings = new CikadaOidcOptions { Enabled = true, Authority = "https://sso.test/", ClientId = "cikada" };
        var monitor = Mock.Of<IOptionsMonitor<CikadaOidcOptions>>(o => o.CurrentValue == settings);
        var codec = new CikadaUserBridgeProof(new EphemeralDataProtectionProvider(), TimeProvider.System);
        var claims = new List<Claim> { new("sub", mode), new("preferred_username", mode == "missing-account" ? "missing" : admin.Username),
            new("iss", settings.Authority), new("aud", settings.ClientId) };
        if (mode != "idp-not-admin") claims.Add(new Claim(ClaimTypes.Role, WarehouseRoles.WarehouseAdmin));
        var principal = new ClaimsPrincipal(CikadaJwtValidator.BuildIdentity(claims, settings, WarehouseAuthenticationDefaults.Scheme));
        if (mode == "unsigned-source") principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim("auth_source", "oauth"),
            new Claim(ClaimTypes.Role, WarehouseRoles.WarehouseAdmin)], WarehouseAuthenticationDefaults.Scheme));
        var http = new DefaultHttpContext { User = principal };
        http.Request.Headers[CikadaUserBridgeProof.HeaderName] = codec.Create(mode == "wrong-actor" ? Guid.NewGuid() : admin.Id, CikadaUserBridgeProof.ManageUsers);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var executed = false;
        await new TrustedUserAdministrationFilter(codec, users, monitor).OnActionExecutionAsync(context,
            () => { executed = true; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
        Assert.Equal(expected, executed);
        Assert.Equal(mode == "unsigned-source" ? admin.Id.ToString() : mode, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!expected) Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

}
