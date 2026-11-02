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
}
