using LKvitai.MES.BuildingBlocks.PortalAuth;
namespace LKvitai.MES.Modules.Warehouse.Api.Security;
public static class LegacySqlMappingEndpoints
{
    public static IEndpointConventionBuilder MapLegacySqlMappingLookup(this IEndpointRouteBuilder app)
        =>app.MapGet("/api/internal/legacy-sql-identity",(HttpContext context,CikadaUserBridgeProof proof,IAdminUserStore users)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var identity=proof.Read(context.Request.Headers[CikadaUserBridgeProof.HeaderName].ToString(),CikadaUserBridgeProof.LookupLegacySql);
            if(identity is null)return Results.StatusCode(401);
            if(!users.TryGetLegacySqlLogin(identity.UserId,out var login))return Results.NotFound();
            return Results.Ok(new {UserId=identity.UserId,LegacySqlLogin=login});
        }).AllowAnonymous(); // Authenticated by the purpose-bound server proof, never unsigned Warehouse role headers.
}
