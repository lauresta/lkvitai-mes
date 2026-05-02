using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.Modules.Frontline.Api.Composition;
using LKvitai.MES.Modules.Frontline.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("frontline");
builder.Services.AddScaffoldApiCore();

// F-1: bind the fabric read-side to the in-memory stub
// (StubFabricQueryService). The default mode is Stub because Frontline has no
// SQL adapter yet — F-2 introduces SqlFabricQueryService over
// dbo.weblb_Fabric_GetMobileCard + a new weblb_Fabric_GetLowStockList proc
// derived from the legacy web_RemainsAll view, at which point the default
// will flip to Sql/Auto.
builder.Services.AddFrontlineFabricDataSource(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseScaffoldApiPipeline();

// TODO: tighten auth when roles are defined. Frontline is the safe field/branch
// surface (e.g. fabric availability); it stays anonymous until the role model
// is finalised, at which point this group will adopt the shared PortalAuth
// scheme and a read-only policy for operators in the field. Until then the
// endpoints are reachable without authentication because this app intentionally
// does not register AddAuthorization()/UseAuthentication/UseAuthorization — no
// AllowAnonymous() metadata is emitted here to avoid suggesting otherwise.
var frontline = app.MapGroup("/api/frontline");

frontline.MapGet("/ping", () => Results.Ok(new
{
    Module = "Frontline",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

frontline.MapFabricAvailabilityEndpoints();

await app.RunAsync();
